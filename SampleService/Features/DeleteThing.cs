using Featurewise;
using Microsoft.AspNetCore.Authorization;

namespace SampleService;

// Deletes a Thing. HandleRestoreDue brings it back.
public class DeleteThing(IThingsPersistence things, IChanges changes) : Command<DeleteThing.Args, DeleteThing.Result>
{
    public record Args(string Id) : Link<DeleteThing, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        // Leads back to the list, so a UI shows that rather than offering to delete it again.
        public record Deleted(Things.Args LinkToThings) : Result;

        public record NotFound(string Id) : Result;
    }

    // Only editors may delete.
    public class Rule : AuthorizationHandler<InvokeRequirement, Args>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, InvokeRequirement requirement, Args link)
        {
            if (context.User.IsInRole("editor"))
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        if (await things.Find(args.Id, cancellationToken) is null)
            return new Result.NotFound(args.Id);

        // Someone else may delete it first, and then the delete's condition fails.
        things.Delete(args.Id);
        if (!await changes.Commit(cancellationToken))
            return new Result.NotFound(args.Id);
        return new Result.Deleted(new Things.Args());
    }
}
