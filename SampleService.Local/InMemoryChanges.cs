namespace SampleService.Local;

// The invocation's unit of work in memory. Commit checks every condition, then makes every write, under one
// lock, so a commit is all or none, as DynamoDB's is. Conditions see the state from before the commit, as
// DynamoDB's do. An invocation that ends with writes it never committed is a bug, so disposing it then throws.
public sealed class InMemoryChanges : IChanges, IDisposable
{
    private static readonly Lock CommitLock = new();

    private readonly List<(Func<bool> Condition, Action Write)> _staged = [];

    public void Stage(Func<bool> condition, Action write) => _staged.Add((condition, write));

    public Task<bool> Commit(CancellationToken cancellationToken)
    {
        lock (CommitLock)
        {
            var staged = _staged.ToList();
            _staged.Clear();
            if (!staged.All(change => change.Condition()))
                return Task.FromResult(false);

            foreach (var change in staged)
                change.Write();
            return Task.FromResult(true);
        }
    }

    public void Dispose()
    {
        if (_staged.Count > 0)
            throw new InvalidOperationException(
                $"the invocation staged {_staged.Count} writes and never committed them (if it also threw, that's the first failure)");
    }
}
