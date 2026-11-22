namespace SampleService;

// Where Things are kept. Reads happen straight away. Writes are staged in the invocation's IChanges and
// happen only when the feature commits.
public interface IThingsPersistence
{
    Task<Thing?> Find(string id, CancellationToken cancellationToken);

    // Every Thing, by id. A sample-sized service can afford to scan; a larger one would query an index.
    Task<IReadOnlyList<Thing>> All(CancellationToken cancellationToken);

    // Stages saving the Thing, whether or not it's there.
    void Put(Thing thing);

    // Stages saving the Thing, on condition that it's still there.
    void Replace(Thing thing);

    // Stages deleting the Thing, on condition that it's still there.
    void Delete(string id);
}
