using System.Security.Claims;
using Amazon.Lambda.APIGatewayEvents;

namespace SampleService.PublicApi;

// The caller of a public API request: the client whose access token API Gateway's JWT authorizer has
// verified, named by its client id, with a claim for each scope the token grants. Without a token, the
// caller is anonymous.
public static class ClientCaller
{
    public const string ScopeClaim = "scope";

    public static ClaimsPrincipal From(APIGatewayHttpApiV2ProxyRequest request)
    {
        var claims = request.RequestContext?.Authorizer?.Jwt?.Claims;
        if (claims is null || !claims.TryGetValue("client_id", out var clientId))
            return new ClaimsPrincipal(new ClaimsIdentity());

        var identity = new ClaimsIdentity(authenticationType: "client-credentials");
        identity.AddClaim(new Claim(ClaimTypes.Name, clientId));
        if (claims.TryGetValue(ScopeClaim, out var scopes))
            foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                identity.AddClaim(new Claim(ScopeClaim, scope));
        return new ClaimsPrincipal(identity);
    }
}
