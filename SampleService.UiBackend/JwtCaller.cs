using System.Security.Claims;
using Amazon.Lambda.APIGatewayEvents;

namespace SampleService.UiBackend;

// The caller of a request, from the claims that API Gateway's JWT authorizer has verified. Without
// an authorizer, or without a token, the caller is anonymous.
public static class JwtCaller
{
    public static ClaimsPrincipal From(APIGatewayHttpApiV2ProxyRequest request)
    {
        var claims = request.RequestContext?.Authorizer?.Jwt?.Claims;
        if (claims is null || claims.Count == 0)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var identity = new ClaimsIdentity(authenticationType: "jwt");
        if ((ValueOf(claims, "username") ?? ValueOf(claims, "sub")) is { } name)
            identity.AddClaim(new Claim(ClaimTypes.Name, name));
        foreach (var group in Groups(ValueOf(claims, "cognito:groups") ?? ValueOf(claims, "groups")))
            identity.AddClaim(new Claim(ClaimTypes.Role, group));
        return new ClaimsPrincipal(identity);
    }

    // API Gateway flattens a token's array claims into one string, such as "[editor admin]".
    private static IEnumerable<string> Groups(string? claim) =>
        claim?.Trim('[', ']').Split([' ', ','], StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static string? ValueOf(IDictionary<string, string> claims, string key) =>
        claims.TryGetValue(key, out var value) ? value : null;
}
