using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace SampleService.Aws;

// Keeps Things in a DynamoDB table keyed by Id, one item per Thing. Writes are staged in the invocation's
// DynamoChanges.
public class DynamoThingsPersistence(IAmazonDynamoDB dynamo, DynamoChanges changes, string tableName) : IThingsPersistence
{
    private const string StillThere = "attribute_exists(Id)";

    public async Task<Thing?> Find(string id, CancellationToken cancellationToken)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = tableName, Key = KeyOf(id), ConsistentRead = true }, cancellationToken);
        return response.Item is { Count: > 0 } item ? FromItem(item) : null;
    }

    public async Task<IReadOnlyList<Thing>> All(CancellationToken cancellationToken)
    {
        var things = new List<Thing>();
        Dictionary<string, AttributeValue>? startKey = null;
        do
        {
            var page = await dynamo.ScanAsync(
                new ScanRequest { TableName = tableName, ExclusiveStartKey = startKey }, cancellationToken);
            things.AddRange((page.Items ?? []).Select(FromItem));
            startKey = page.LastEvaluatedKey is { Count: > 0 } lastKey ? lastKey : null;
        } while (startKey is not null);

        return things.OrderBy(thing => thing.Id, StringComparer.Ordinal).ToList();
    }

    public void Put(Thing thing) =>
        changes.Stage(new TransactWriteItem { Put = new Put { TableName = tableName, Item = ToItem(thing) } });

    public void Replace(Thing thing) =>
        changes.Stage(new TransactWriteItem
        {
            Put = new Put { TableName = tableName, Item = ToItem(thing), ConditionExpression = StillThere }
        });

    public void Delete(string id) =>
        changes.Stage(new TransactWriteItem
        {
            Delete = new Delete { TableName = tableName, Key = KeyOf(id), ConditionExpression = StillThere }
        });

    private static Dictionary<string, AttributeValue> KeyOf(string id) => new() { ["Id"] = new AttributeValue { S = id } };

    private static Dictionary<string, AttributeValue> ToItem(Thing thing) =>
        new()
        {
            ["Id"] = new() { S = thing.Id },
            ["Name"] = new() { S = thing.Name }
        };

    private static Thing FromItem(Dictionary<string, AttributeValue> item) => new(item["Id"].S, item["Name"].S);
}
