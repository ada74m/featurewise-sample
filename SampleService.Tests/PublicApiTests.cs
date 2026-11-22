using System.Net;
using System.Text.Json.Nodes;
using Amazon.Lambda.APIGatewayEvents;
using SampleService.PublicApi;
using YamlDotNet.RepresentationModel;

namespace SampleService.Tests;

public class PublicApiTests
{
    private const string GetThings = "GET /v1/things";

    private readonly Fakes _fakes = new Fakes().WithSeed();
    private readonly Function _api;

    public PublicApiTests() => _api = new Function(_fakes.Register);

    [Fact]
    public async Task ThingsAreReadInThePublicShape()
    {
        _fakes.Things.Remove("t2");
        _fakes.Things.Remove("t3");
        _fakes.Things.Remove("t4");
        _fakes.Things.Remove("t5");

        var response = await Handle(Lambda.Route(GetThings, null, Lambda.Partner));

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Headers["content-type"]);
        Assert.Equal("""{"things":[{"id":"t1","name":"Teapot"}]}""", response.Body);
    }

    [Fact]
    public async Task WithoutATokenTheCallerIsUnauthorizedAndWithoutTheScopeForbidden()
    {
        var anonymous = await Handle(Lambda.Route(GetThings, null, null));
        var unscoped = await Handle(Lambda.Route(GetThings, null, Lambda.PartnerWithoutScope));

        Assert.Equal((int)HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, unscoped.StatusCode);
        Assert.Equal("application/problem+json", unscoped.Headers["content-type"]);
        Assert.Equal(403, (int?)JsonNode.Parse(unscoped.Body)!["status"]);
    }

    [Fact]
    public async Task ARouteTheApiDoesntHaveIsNotFound()
    {
        var response = await Handle(Lambda.Route("DELETE /v1/things/{thingId}", new() { ["thingId"] = "t1" }, Lambda.Partner));

        Assert.Equal((int)HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(_fakes.Things["t1"]);
    }

    [Fact]
    public void TheSpecDescribesExactlyTheRoutesTheFunctionHandles()
    {
        var spec = new YamlStream();
        spec.Load(new StringReader(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "openapi.yaml"))));
        var paths = (YamlMappingNode)((YamlMappingNode)spec.Documents[0].RootNode)["paths"];
        string[] methods = ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

        var operations = paths.Children
            .SelectMany(path => ((YamlMappingNode)path.Value).Children.Keys
                .Select(key => key.ToString())
                .Where(methods.Contains)
                .Select(method => $"{method.ToUpperInvariant()} {path.Key}"));

        Assert.Equal(_api.Routes.Order(), operations.Order());
    }

    private Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request) =>
        _api.Handle(request, Lambda.Context());
}
