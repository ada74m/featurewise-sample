namespace SampleService.Local;

// Reads Things from the shared InMemoryThings, and stages writes to it in the invocation's InMemoryChanges.
public class InMemoryThingsPersistence(InMemoryThings things, InMemoryChanges changes) : IThingsPersistence
{
    public Task<Thing?> Find(string id, CancellationToken cancellationToken) => Task.FromResult(things[id]);

    public Task<IReadOnlyList<Thing>> All(CancellationToken cancellationToken) => Task.FromResult(things.All());

    public void Put(Thing thing) => changes.Stage(() => true, () => things.Put(thing));

    public void Replace(Thing thing) => changes.Stage(() => things[thing.Id] is not null, () => things.Put(thing));

    public void Delete(string id) => changes.Stage(() => things[id] is not null, () => things.Remove(id));
}
