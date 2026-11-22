using System.Text;
using System.Text.Json.Nodes;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.FileProviders;
using SampleService;
using SampleService.Local;
using PublicApi = SampleService.PublicApi.Function;
using ScheduledWorker = SampleService.ScheduledWorker.Function;
using UiBackend = SampleService.UiBackend.Function;

// Runs the whole service in one process, over facilities in memory, so it can be used without
// deploying it. Each request becomes the event API Gateway would send the deployed function, and goes to
// the same Function class, so the code under test locally is the code that's deployed:
//
//   POST /api/sample/{feature}          the UI backend, as the UI shell will reach it through CloudFront
//   GET  /api/sample/{feature}?link=    the UI backend, for queries
//   GET  /public/sample/v1/...          the public API's routes
//   POST /jobs                          runs the job whose link is the body, as a schedule would
//
// It also serves the UI, as CloudFront will in AWS, all from one origin, so there's no CORS:
//
//   GET  /services.json                 the shell's configuration, with the users to sign in as
//   GET  /modules/sample/...            the service's module, built in SampleService.Ui/dist
//   GET  anything else                  the shell, from a sibling checkout of featurewise-ui, ../ui/dist
//
// Callers sign in with a bearer token that isn't checked; see UnsignedJwt. The Things are restored when
// the host starts, as they are when the service is first deployed.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Where the built shell and module are, which the settings ShellFiles and ModuleFiles can change.
var shellDirectory = Path.GetFullPath(builder.Configuration["ShellFiles"] ?? Path.Combine(builder.Environment.ContentRootPath, "../../ui/dist"));
var moduleDirectory = Path.GetFullPath(builder.Configuration["ModuleFiles"] ?? Path.Combine(builder.Environment.ContentRootPath, "../SampleService.Ui/dist"));

if (Directory.Exists(moduleDirectory))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(moduleDirectory), RequestPath = "/modules/sample" });
if (Directory.Exists(shellDirectory))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(shellDirectory) });

var register = Registrations.Over(new InMemoryThings());
var uiBackend = new UiBackend(register);
var publicApi = new PublicApi(register);
var scheduledWorker = new ScheduledWorker(register);

await RunJob(new JsonObject { ["$worker"] = $"{Service.Id}/HandleRestoreDue" });

app.MapMethods("/api/sample/{feature}", ["POST", "GET"], async (HttpContext http, string feature) =>
{
    var request = await ToEvent(http.Request, $"{http.Request.Method} /{{feature}}", new() { ["feature"] = feature });
    await Write(http.Response, await uiBackend.Handle(request, new LocalLambdaContext()));
});

foreach (var route in publicApi.Routes)
{
    var method = route[..route.IndexOf(' ')];
    var template = route[(route.IndexOf(' ') + 1)..];
    app.MapMethods($"/public/sample{template}", [method], async (HttpContext http) =>
    {
        var pathParameters = http.Request.RouteValues.ToDictionary(value => value.Key, value => value.Value?.ToString() ?? "");
        var request = await ToEvent(http.Request, route, pathParameters);
        await Write(http.Response, await publicApi.Handle(request, new LocalLambdaContext()));
    });
}

app.MapPost("/jobs", async (HttpContext http) =>
{
    if (await JsonNode.ParseAsync(http.Request.Body) is not JsonObject link)
        return Results.BadRequest("the body must be a job's link");

    await RunJob(link);
    return Results.NoContent();
});

app.MapGet("/services.json", () => Results.Json(new
{
    @default = "/sample/things",
    services = new Dictionary<string, object> { [Service.Id] = new { module = "/modules/sample/index.js", bff = "/api/sample" } },
    // The dev users the shell's pill offers: an editor, who may delete Things, and someone who may not.
    devUsers = new object[] { new { username = "ann", groups = new[] { "editor" } }, new { username = "bob" } }
}));

// Any other page is an address for the shell, which reads the URL itself.
app.MapFallback(async (HttpContext http) =>
{
    var index = Path.Combine(shellDirectory, "index.html");
    if (http.Request.Method != "GET" || !File.Exists(index))
    {
        http.Response.StatusCode = StatusCodes.Status404NotFound;
        await http.Response.WriteAsync(File.Exists(index) ? "" : $"The shell isn't built: run npm run build in {Path.GetDirectoryName(shellDirectory)}.");
        return;
    }
    http.Response.ContentType = "text/html";
    await http.Response.SendFileAsync(index);
});

app.Run();

// As EventBridge Scheduler invokes the scheduled worker, from a schedule named local.
async Task RunJob(JsonObject link)
{
    var input = new JsonObject { ["Schedule"] = "local", ["Link"] = link };
    await scheduledWorker.Handle(new MemoryStream(Encoding.UTF8.GetBytes(input.ToJsonString())), new LocalLambdaContext());
}

static async Task<APIGatewayHttpApiV2ProxyRequest> ToEvent(
    HttpRequest http, string routeKey, Dictionary<string, string> pathParameters)
{
    var body = await new StreamReader(http.Body).ReadToEndAsync();
    var claims = UnsignedJwt.Claims(http.Headers.Authorization);
    return new APIGatewayHttpApiV2ProxyRequest
    {
        RouteKey = routeKey,
        RawPath = http.Path,
        PathParameters = pathParameters,
        // API Gateway joins a parameter's repeated values with commas.
        QueryStringParameters = http.Query.ToDictionary(parameter => parameter.Key, parameter => parameter.Value.ToString()),
        // HTTP APIs give header names in lower case.
        Headers = http.Headers.ToDictionary(header => header.Key.ToLowerInvariant(), header => header.Value.ToString()),
        Body = body.Length == 0 ? null : body,
        RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
        {
            Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = http.Method },
            Authorizer = claims is null
                ? null
                : new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Jwt = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription { Claims = claims }
                }
        }
    };
}

static async Task Write(HttpResponse http, APIGatewayHttpApiV2ProxyResponse response)
{
    http.StatusCode = response.StatusCode;
    foreach (var (name, value) in response.Headers ?? new Dictionary<string, string>())
        http.Headers[name] = value;
    if (response.Body is not null)
        await http.WriteAsync(response.Body);
}

// Lets the tests start the host.
public partial class Program;
