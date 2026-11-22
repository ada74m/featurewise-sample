using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace SampleService.UiBackend;

// The backend for the service's own UI: invokes its queries and commands over API Gateway's HTTP API.
// A query is GET /{feature}?link=<its link as JSON>, which a browser may repeat, as when a page is
// reloaded; a command is POST /{feature} with its link as the body, which a browser never repeats, so a
// reload never runs one again. A query may be posted too, but a command can't be got. A link says its
// feature's family, so the UI knows which to use before it follows one.
// Its contract is the features themselves, links and result cases included, which suits a UI shipped
// with the service and no one else.
public class Function
{
    // Problems are the backend's own shape, in RFC 9457's camelCase, not a feature's result.
    private static readonly JsonSerializerOptions ProblemJson = new(JsonSerializerDefaults.Web);

    private readonly Features _features = new(Service.Id);
    private readonly JsonSerializerOptions _json;

    public Function() : this(Aws.Registrations.Register)
    {
    }

    public Function(Action<IServiceCollection> registerDependencies)
    {
        _features
            .RegisterFeature<Things, AnySignedInCaller<Things.Args>>()
            .RegisterFeature<DeleteThing, DeleteThing.Rule>()
            .RegisterFeature<RenameThing, AnySignedInCaller<RenameThing.Args>>();

        _json = _features.ConfigureJson(new JsonSerializerOptions());

        _features
            .RegisterDependencies(services => services.AddSingleton(_json))
            .RegisterDependencies(registerDependencies)
            .Build();
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        // The URL names the service and the path the feature, which a link qualifies with the service, as
        // in "$command":"sample/DeleteThing".
        var feature = ValueOf(request.PathParameters, "feature") ?? "";
        var qualified = $"{Service.Id}/{feature}";
        if (!_features.IsInvocable(qualified))
            return Problem(HttpStatusCode.NotFound, $"there is no feature named {feature}");

        var get = request.RequestContext?.Http?.Method == "GET";
        if (get && !_features.IsQuery(qualified))
            return Problem(HttpStatusCode.MethodNotAllowed, $"{feature} isn't a query, so it's invoked only by POST", ("allow", "POST"));

        // HTTP APIs give header names in lower case.
        var idempotencyKey = ValueOf(request.Headers, "idempotency-key");
        if (idempotencyKey is not null && string.IsNullOrWhiteSpace(idempotencyKey))
            return Problem(HttpStatusCode.BadRequest, "the Idempotency-Key header is blank");

        Link link;
        try
        {
            link = ReadLink(qualified, get ? QueryArguments(request) : BodyArguments(request));
        }
        catch (JsonException e)
        {
            return Problem(HttpStatusCode.BadRequest, e.Message);
        }

        using var timeout = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1));
        try
        {
            var result = await _features.Invoke(link, JwtCaller.From(request), idempotencyKey, timeout.Token);
            // The feature's family, so the UI gives a query an address, and never a command.
            return Response(HttpStatusCode.OK, JsonSerializer.Serialize(result, _json),
                headers: ("featurewise-family", _features.FamilyOf(qualified)!.Value.ToString().ToLowerInvariant()));
        }
        catch (InvocationDeniedException denied)
        {
            return denied.CallerAuthenticated
                ? Problem(HttpStatusCode.Forbidden, $"you may not invoke {feature}")
                : Problem(HttpStatusCode.Unauthorized, $"sign in to invoke {feature}");
        }
    }

    // The link to the qualified feature that the arguments make. The UI may post bare arguments as well as
    // a link it was given, which must name the same feature, in the property its family uses ($query,
    // $command or $worker; $feature in a link stored before links said their family).
    private Link ReadLink(string qualified, JsonObject arguments)
    {
        var property = Features.FeatureProperty(_features.FamilyOf(qualified)!.Value);
        foreach (var family in Enum.GetValues<FeatureFamily>().Select(Features.FeatureProperty).Append("$feature"))
        {
            if (!arguments.TryGetPropertyValue(family, out var named)) continue;
            if (!(named is JsonValue value && value.TryGetValue<string>(out var name) && name == qualified))
                throw new JsonException($"the arguments name a feature other than {qualified}");
            if (family != property && family != "$feature")
                throw new JsonException($"{qualified} is named in {property}, not {family}");
            arguments.Remove(family);
        }

        arguments[property] = qualified;
        return arguments.Deserialize<Link>(_json) ?? throw new JsonException("the arguments are null");
    }

    private static JsonObject BodyArguments(APIGatewayHttpApiV2ProxyRequest request)
    {
        var body = request.IsBase64Encoded && request.Body is not null
            ? Encoding.UTF8.GetString(Convert.FromBase64String(request.Body))
            : request.Body;
        if (string.IsNullOrWhiteSpace(body))
            return new JsonObject();

        return JsonNode.Parse(body) as JsonObject ?? throw new JsonException("the body must be a JSON object");
    }

    private static JsonObject QueryArguments(APIGatewayHttpApiV2ProxyRequest request)
    {
        var link = ValueOf(request.QueryStringParameters, "link");
        if (string.IsNullOrWhiteSpace(link))
            return new JsonObject();

        return JsonNode.Parse(link) as JsonObject ?? throw new JsonException("the link must be a JSON object");
    }

    private static string? ValueOf(IDictionary<string, string>? values, string key) =>
        values is not null && values.TryGetValue(key, out var value) ? value : null;

    private static APIGatewayHttpApiV2ProxyResponse Problem(
        HttpStatusCode status, string detail, params (string Name, string Value)[] headers) =>
        Response(
            status,
            JsonSerializer.Serialize(new Problem(TitleOf(status), (int)status, detail), ProblemJson),
            "application/problem+json",
            headers);

    private static string TitleOf(HttpStatusCode status) =>
        status switch
        {
            HttpStatusCode.BadRequest => "Bad request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not found",
            HttpStatusCode.MethodNotAllowed => "Method not allowed",
            _ => status.ToString()
        };

    private static APIGatewayHttpApiV2ProxyResponse Response(
        HttpStatusCode status, string json, string contentType = "application/json",
        params (string Name, string Value)[] headers) =>
        new()
        {
            StatusCode = (int)status,
            Body = json,
            Headers = headers.Append(("content-type", contentType)).ToDictionary(h => h.Item1, h => h.Item2)
        };
}
