using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace SampleService.PublicApi;

// The service's API for other organisations' systems, on API Gateway's HTTP API. Unlike the UI backend,
// its contract is its own, described in openapi.yaml: resources under a version, HTTP status codes for
// outcomes, and public shapes that leave out links, result cases and internal fields. Each route builds
// a link, invokes it, and turns the result into a response.
public class Function
{
    private delegate Task<APIGatewayHttpApiV2ProxyResponse> Route(
        APIGatewayHttpApiV2ProxyRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Features _features = new(Service.Id);
    private readonly Dictionary<string, Route> _routes;

    public Function() : this(Aws.Registrations.Register)
    {
    }

    public Function(Action<IServiceCollection> registerDependencies)
    {
        _routes = new Dictionary<string, Route>
        {
            ["GET /v1/things"] = GetThings
        };

        _features
            .RegisterFeature<Things, ThingsReadRule>()
            .RegisterDependencies(registerDependencies)
            .Build();
    }

    // The routes this function handles, as API Gateway names them: the operations in openapi.yaml.
    public IReadOnlyCollection<string> Routes => _routes.Keys;

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        if (request.RouteKey is null || !_routes.TryGetValue(request.RouteKey, out var route))
            return Problem(HttpStatusCode.NotFound, "Not found", $"There is no route {request.RouteKey}.");

        using var timeout = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1));
        try
        {
            return await route(request, ClientCaller.From(request), timeout.Token);
        }
        catch (InvocationDeniedException denied)
        {
            return denied.CallerAuthenticated
                ? Problem(HttpStatusCode.Forbidden, "Forbidden", "The client's token doesn't grant this operation.")
                : Problem(HttpStatusCode.Unauthorized, "Unauthorized", "The request needs a client's access token.");
        }
    }

    // A client may repeat a GET, so a GET route invokes only a query.
    private async Task<APIGatewayHttpApiV2ProxyResponse> GetThings(
        APIGatewayHttpApiV2ProxyRequest request, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var result = await _features.Invoke(new Things.Args(), caller, cancellationToken);
        return Response(HttpStatusCode.OK, new { things = result.Things.Select(entry => ThingView.From(entry.Thing)) });
    }

    private static APIGatewayHttpApiV2ProxyResponse Response<T>(HttpStatusCode status, T body) =>
        new()
        {
            StatusCode = (int)status,
            Body = JsonSerializer.Serialize(body, Json),
            Headers = new Dictionary<string, string> { ["content-type"] = "application/json" }
        };

    private static APIGatewayHttpApiV2ProxyResponse Problem(HttpStatusCode status, string title, string detail) =>
        new()
        {
            StatusCode = (int)status,
            Body = JsonSerializer.Serialize(new Problem(title, (int)status, detail), Json),
            Headers = new Dictionary<string, string> { ["content-type"] = "application/problem+json" }
        };
}
