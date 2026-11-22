using Featurewise;

namespace SampleService;

// Gives a Thing a new name. A link with the new name null, as Things offers, asks for one: invoking it
// returns NeedsNewName, with the Thing as it is and the link back, so a UI can ask for the name and follow
// the link with it.
public class RenameThing(IThingsPersistence things, IChanges changes) : Command<RenameThing.Args, RenameThing.Result>
{
    public record Args(string Id, string? NewName) : Link<RenameThing, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public record Renamed(Thing Thing, Things.Args LinkToThings) : Result;

        public record NeedsNewName(Thing Thing, Args LinkToRename) : Result;

        public record NotFound(string Id) : Result;
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        if (await things.Find(args.Id, cancellationToken) is not { } thing)
            return new Result.NotFound(args.Id);
        if (string.IsNullOrWhiteSpace(args.NewName))
            return new Result.NeedsNewName(thing, args);

        var renamed = thing with { Name = args.NewName.Trim() };
        things.Replace(renamed);
        if (!await changes.Commit(cancellationToken))
            return new Result.NotFound(args.Id);
        return new Result.Renamed(renamed, new Things.Args());
    }
}
