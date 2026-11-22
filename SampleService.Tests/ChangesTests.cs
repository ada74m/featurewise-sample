using SampleService.Local;

namespace SampleService.Tests;

// The in-memory unit of work, which the local host and every other test rely on to behave as DynamoDB's does.
public class ChangesTests
{
    private readonly InMemoryThings _things = new();

    [Fact]
    public async Task NothingIsWrittenUntilTheCommit()
    {
        using var changes = new InMemoryChanges();
        var persistence = new InMemoryThingsPersistence(_things, changes);

        persistence.Put(new Thing("t1", "Teapot"));
        Assert.Null(_things["t1"]);

        Assert.True(await changes.Commit(CancellationToken.None));
        Assert.Equal("Teapot", _things["t1"]!.Name);
    }

    [Fact]
    public async Task ACommitWhoseConditionNoLongerHoldsWritesNothing()
    {
        _things.Put(new Thing("t1", "Teapot"));
        _things.Put(new Thing("t2", "Lighthouse"));
        using var changes = new InMemoryChanges();
        var persistence = new InMemoryThingsPersistence(_things, changes);

        persistence.Replace(new Thing("t1", "Kettle"));
        persistence.Delete("t2");
        _things.Remove("t2");   // someone else got there first

        Assert.False(await changes.Commit(CancellationToken.None));
        Assert.Equal("Teapot", _things["t1"]!.Name);
    }

    [Fact]
    public void AnInvocationThatNeverCommitsItsWritesFails()
    {
        var changes = new InMemoryChanges();
        new InMemoryThingsPersistence(_things, changes).Put(new Thing("t1", "Teapot"));

        Assert.Throws<InvalidOperationException>(changes.Dispose);
        Assert.Null(_things["t1"]);
    }
}
