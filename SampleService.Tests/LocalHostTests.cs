using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SampleService.Tests;

// The local host, over HTTP, as a developer or the UI shell reaches it.
public class LocalHostTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _host = new();
    private readonly HttpClient _client;

    public LocalHostTests() => _client = _host.CreateClient();

    [Fact]
    public async Task TheUiBackendAnswersAsItsCallersUnsignedTokenSays()
    {
        var editor = await Things(Token("""{"username":"ann","cognito:groups":["editor"]}"""));
        var viewer = await Things(Token("""{"username":"bob"}"""));

        Assert.Equal(HandleRestoreDue.Seed.Count, editor.Count);
        Assert.NotNull(editor[0]!["LinkToDelete"]);
        Assert.Null(viewer[0]!["LinkToDelete"]);
    }

    [Fact]
    public async Task TheUiBackendAnswersAQueryByGetWithItsLinkInTheQueryString()
    {
        var link = Uri.EscapeDataString("""{"$query":"sample/Things"}""");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/sample/Things?link={link}");
        request.Headers.Authorization = Token("""{"username":"bob"}""");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WithoutATokenTheUiBackendSaysToSignIn()
    {
        var response = await _client.PostAsync("/api/sample/Things", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ThePublicApisRoutesAreServedUnderItsPrefix()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/public/sample/v1/things");
        request.Headers.Authorization = Token("""{"client_id":"partner","scope":"sample/things.read"}""");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HandleRestoreDue.Seed.Count, JsonNode.Parse(await response.Content.ReadAsStringAsync())!["things"]!.AsArray().Count);
    }

    [Fact]
    public async Task AJobPostedToTheHostRuns()
    {
        var editor = Token("""{"username":"ann","cognito:groups":["editor"]}""");
        var delete = new HttpRequestMessage(HttpMethod.Post, "/api/sample/DeleteThing") { Content = Json("""{"Id":"t1"}""") };
        delete.Headers.Authorization = editor;
        await _client.SendAsync(delete);

        var job = await _client.PostAsync("/jobs", Json("""{"$worker":"sample/HandleRestoreDue"}"""));

        Assert.Equal(HttpStatusCode.NoContent, job.StatusCode);
        Assert.Equal(HandleRestoreDue.Seed.Count, (await Things(editor)).Count);
    }

    [Fact]
    public async Task TheShellsConfigurationNamesTheServiceItsModuleAndBackend()
    {
        var config = JsonNode.Parse(await _client.GetStringAsync("/services.json"))!;

        Assert.Equal("/sample/things", (string?)config["default"]);
        Assert.Equal("/api/sample", (string?)config["services"]!["sample"]!["bff"]);
        Assert.Equal("/modules/sample/index.js", (string?)config["services"]!["sample"]!["module"]);
        Assert.Equal("ann", (string?)config["devUsers"]![0]!["username"]);
    }

    [Fact]
    public async Task TheShellIsServedAtEveryOtherPathAndTheModuleUnderModules()
    {
        var shell = Directory.CreateTempSubdirectory();
        var module = Directory.CreateTempSubdirectory();
        File.WriteAllText(Path.Combine(shell.FullName, "index.html"), "<main id=featurewise></main>");
        File.WriteAllText(Path.Combine(shell.FullName, "main.js"), "// the shell");
        File.WriteAllText(Path.Combine(module.FullName, "index.js"), "// the module");
        using var host = _host.WithWebHostBuilder(web => web.UseSetting("ShellFiles", shell.FullName).UseSetting("ModuleFiles", module.FullName));
        using var client = host.CreateClient();

        try
        {
            Assert.Contains("featurewise", await client.GetStringAsync("/sample/things"));
            Assert.Equal("// the shell", await client.GetStringAsync("/main.js"));
            Assert.Equal("// the module", await client.GetStringAsync("/modules/sample/index.js"));
            // The fallback doesn't swallow the backend: unsigned, it says to sign in.
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/sample/Things", null)).StatusCode);
        }
        finally
        {
            shell.Delete(recursive: true);
            module.Delete(recursive: true);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
    }

    private async Task<JsonArray> Things(AuthenticationHeaderValue token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sample/Things");
        request.Headers.Authorization = token;
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["Things"]!.AsArray();
    }

    // A token with the given claims, unsigned, as a developer makes one for the local host.
    private static AuthenticationHeaderValue Token(string claims) =>
        new("Bearer", $"none.{Convert.ToBase64String(Encoding.UTF8.GetBytes(claims)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.none");

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
