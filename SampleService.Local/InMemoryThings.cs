using System.Collections.Concurrent;

namespace SampleService.Local;

// Keeps Things in memory in place of DynamoDB's table, for the local host and the tests. It's shared, as
// the table is; the tests seed and inspect it directly.
public class InMemoryThings
{
    private readonly ConcurrentDictionary<string, Thing> _things = new();

    public Thing? this[string id] => _things.GetValueOrDefault(id);

    public int Count => _things.Count;

    public IReadOnlyList<Thing> All() => _things.Values.OrderBy(thing => thing.Id, StringComparer.Ordinal).ToList();

    public void Put(Thing thing) => _things[thing.Id] = thing;

    public void Remove(string id) => _things.TryRemove(id, out _);
}
