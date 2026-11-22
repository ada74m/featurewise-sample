using System.Security.Cryptography;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Featurewise;

namespace SampleService.Aws;

// The invocation's unit of work on DynamoDB. The facilities stage their writes, and Commit sends them as
// one TransactWriteItems request, which DynamoDB applies all or none. Nothing has happened until then.
public class DynamoChanges(IAmazonDynamoDB dynamo, Invocation invocation) : IChanges
{
    private readonly List<TransactWriteItem> _staged = [];

    public void Stage(TransactWriteItem write) => _staged.Add(write);

    public async Task<bool> Commit(CancellationToken cancellationToken)
    {
        if (_staged.Count == 0)
            return true;

        var request = new TransactWriteItemsRequest
        {
            TransactItems = [.. _staged],
            ClientRequestToken = TokenFor(invocation.IdempotencyKey)
        };
        _staged.Clear();

        try
        {
            await dynamo.TransactWriteItemsAsync(request, cancellationToken);
            return true;
        }
        catch (TransactionCanceledException cancelled)
            when (cancelled.CancellationReasons?.Any(reason => reason.Code == "ConditionalCheckFailed") == true)
        {
            return false;
        }
    }

    // DynamoDB treats a repeated request with the same token as done for ten minutes, so a retried
    // invocation commits once. A token is at most 36 characters, and an idempotency key may be longer.
    private static string? TokenFor(string? idempotencyKey) =>
        idempotencyKey is null
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)))[..32];
}
