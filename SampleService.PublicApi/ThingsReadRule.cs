using Featurewise;
using Microsoft.AspNetCore.Authorization;

namespace SampleService.PublicApi;

// Clients whose token grants sample/things.read may read Things through the public API. API Gateway
// checks the scope too; this keeps a misconfigured route from opening the feature.
public class ThingsReadRule : AuthorizationHandler<InvokeRequirement, Things.Args>
{
    public const string Scope = "sample/things.read";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InvokeRequirement requirement, Things.Args link)
    {
        if (context.User.HasClaim(ClientCaller.ScopeClaim, Scope))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
