using Featurewise;
using Microsoft.Extensions.Logging;

namespace SampleService;

// Puts the Things back as they started, on a schedule, so a demo that visitors delete and rename never
// stays empty: deleted ones return, renamed ones get their names back, and any others go.
public class HandleRestoreDue(IThingsPersistence things, IChanges changes, ILogger<HandleRestoreDue> logger)
    : Worker<HandleRestoreDue.Args>
{
    public record Args : Link<HandleRestoreDue, Nil>;

    public static IReadOnlyList<Thing> Seed { get; } =
    [
        new("t1", "Teapot"),
        new("t2", "Lighthouse"),
        new("t3", "Umbrella"),
        new("t4", "Harmonica"),
        new("t5", "Compass")
    ];

    public override async Task<Nil> Invoke(Args args, CancellationToken cancellationToken)
    {
        var current = (await things.All(cancellationToken)).ToDictionary(thing => thing.Id);

        var removed = 0;
        foreach (var id in current.Keys.Except(Seed.Select(thing => thing.Id)))
        {
            things.Delete(id);
            removed++;
        }

        var restored = 0;
        foreach (var thing in Seed.Where(thing => current.GetValueOrDefault(thing.Id) != thing))
        {
            things.Put(thing);
            restored++;
        }

        // Fails only if a visitor deleted one of the others meanwhile. Nothing is written then, and the
        // invocation fails, so the next run starts again from what's there.
        if (!await changes.Commit(cancellationToken))
            throw new InvalidOperationException("the Things changed while they were being restored");

        logger.LogInformation("Restored {Restored} Things and removed {Removed}", restored, removed);
        return Nil.Value;
    }
}
