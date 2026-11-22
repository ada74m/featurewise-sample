using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;

namespace SampleService.Tests;

// Lambda contexts and events, shaped as AWS delivers them.
public static class Lambda
{
    public static TestLambdaContext Context() => new() { RemainingTime = TimeSpan.FromSeconds(30) };

    // JWT claims as API Gateway's authorizer passes them: Ann the editor, and Bob, who isn't one.
    public static readonly Dictionary<string, string> Editor = new() { ["username"] = "ann", ["cognito:groups"] = "[editor]" };

    public static readonly Dictionary<string, string> Viewer = new() { ["username"] = "bob" };

    // A request to the UI backend's one route, POST /{feature}.
    public static APIGatewayHttpApiV2ProxyRequest Post(
        string feature, Dictionary<string, string>? claims, string? body, string? idempotencyKey = null) =>
        new()
        {
            RawPath = $"/{feature}",
            PathParameters = new Dictionary<string, string> { ["feature"] = feature },
            Headers = idempotencyKey is null ? null : new Dictionary<string, string> { ["idempotency-key"] = idempotencyKey },
            Body = body,
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "POST" },
                Authorizer = claims is null
                    ? null
                    : new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                    {
                        Jwt = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription { Claims = claims }
                    }
            }
        };

    // A request to the UI backend's other route, GET /{feature}, with a link in the query string.
    public static APIGatewayHttpApiV2ProxyRequest Get(string feature, Dictionary<string, string>? claims, string? link)
    {
        var request = Post(feature, claims, null);
        request.RequestContext.Http.Method = "GET";
        request.QueryStringParameters = link is null ? null : new Dictionary<string, string> { ["link"] = link };
        return request;
    }

    // Access-token claims for third-party clients, as API Gateway's JWT authorizer passes them: one
    // granted the public API's read scope, and one granted something else.
    public static readonly Dictionary<string, string> Partner = new() { ["client_id"] = "partner", ["scope"] = "sample/things.read" };

    public static readonly Dictionary<string, string> PartnerWithoutScope = new() { ["client_id"] = "partner", ["scope"] = "sample/other.read" };

    // A request to one of the public API's routes, such as "GET /v1/things".
    public static APIGatewayHttpApiV2ProxyRequest Route(
        string routeKey, Dictionary<string, string>? pathParameters, Dictionary<string, string>? claims) =>
        new()
        {
            RouteKey = routeKey,
            PathParameters = pathParameters,
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = routeKey.Split(' ')[0] },
                Authorizer = claims is null
                    ? null
                    : new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                    {
                        Jwt = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription { Claims = claims }
                    }
            }
        };
}
