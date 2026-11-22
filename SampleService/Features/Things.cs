using Featurewise;

namespace SampleService;

// Every Thing, each with the links its caller may follow: to delete it, and to rename it.
public class Things(IThingsPersistence things, LinkPermissions permissions) : Query<Things.Args, Things.Result>
{
    public record Args : Link<Things, Result>;

    public record Result(IReadOnlyList<Entry> Things);

    // The rename link leaves the new name null, for the caller to supply.
    public record Entry(Thing Thing, DeleteThing.Args? LinkToDelete, RenameThing.Args? LinkToRename);

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var entries = new List<Entry>();
        foreach (var thing in await things.All(cancellationToken))
            entries.Add(new Entry(
                thing,
                await Offer(new DeleteThing.Args(thing.Id)),
                await Offer(new RenameThing.Args(thing.Id, null))));

        return new Result(entries);
    }

    private async Task<TLink?> Offer<TLink>(TLink link) where TLink : Link =>
        await permissions.MayInvoke(link) ? link : null;
}
