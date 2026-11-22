using System.Net;
using System.Text.Json.Nodes;
using Amazon.Lambda.APIGatewayEvents;
using SampleService.UiBackend;

namespace SampleService.Tests;

public class UiBackendTests
{
    private readonly Fakes _fakes = new Fakes().WithSeed();
    private readonly Function _uiBackend;

    public UiBackendTests() => _uiBackend = new Function(_fakes.Register);

    [Fact]
    public async Task AQueryIsInvokedByPostAndItsResultCarriesQualifiedLinks()
    {
        var response = await Handle(Lambda.Post("Things", Lambda.Editor, null));

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        var teapot = JsonNode.Parse(response.Body)!["Things"]![0]!;
        Assert.Equal("Teapot", (string?)teapot["Thing"]!["Name"]);
        Assert.Equal("sample/DeleteThing", (string?)teapot["LinkToDelete"]!["$command"]);
        Assert.Equal("t1", (string?)teapot["LinkToDelete"]!["Id"]);
    }

    [Fact]
    public async Task ALinkWithANameStillToBeSuppliedCarriesItAsNull()
    {
        var response = await Handle(Lambda.Post("Things", Lambda.Editor, null));

        var rename = JsonNode.Parse(response.Body)!["Things"]![0]!["LinkToRename"]!.AsObject();
        Assert.Equal("sample/RenameThing", (string?)rename["$command"]);
        Assert.True(rename.TryGetPropertyValue("NewName", out var newName));
        Assert.Null(newName);
    }

    [Fact]
    public async Task AnOfferedLinkIsFollowedByPostingItBackAsItCame()
    {
        var things = await Handle(Lambda.Post("Things", Lambda.Editor, null));
        var link = JsonNode.Parse(things.Body)!["Things"]![0]!["LinkToDelete"]!;

        // As the UI shell does: the part before the slash says which service's backend to post it to.
        var feature = ((string)link["$command"]!).Split('/')[1];
        var deleted = await Handle(Lambda.Post(feature, Lambda.Editor, link.ToJsonString()));

        var body = JsonNode.Parse(deleted.Body)!;
        Assert.Equal("Deleted", (string?)body["$case"]);
        Assert.Equal("sample/Things", (string?)body["LinkToThings"]!["$query"]);
        Assert.Null(_fakes.Things["t1"]);
    }

    [Fact]
    public async Task FollowingARenameLinkAsOfferedAsksForTheNewNameAndFillingItInRenames()
    {
        var asked = await Handle(Lambda.Post("RenameThing", Lambda.Viewer, """{"Id":"t1","NewName":null}"""));
        var renamed = await Handle(Lambda.Post("RenameThing", Lambda.Viewer, """{"Id":"t1","NewName":"Kettle"}"""));

        Assert.Equal("NeedsNewName", (string?)JsonNode.Parse(asked.Body)!["$case"]);
        Assert.Equal("Renamed", (string?)JsonNode.Parse(renamed.Body)!["$case"]);
        Assert.Equal("Kettle", _fakes.Things["t1"]!.Name);
    }

    [Fact]
    public async Task AResultSaysWhichFamilyTheFeatureThatAnsweredBelongsTo()
    {
        var things = await Handle(Lambda.Post("Things", Lambda.Editor, null));
        var deleted = await Handle(Lambda.Post("DeleteThing", Lambda.Editor, """{"Id":"t1"}"""));

        Assert.Equal("query", things.Headers["featurewise-family"]);
        Assert.Equal("command", deleted.Headers["featurewise-family"]);
    }

    [Fact]
    public async Task AQueryIsInvokedByGetToo()
    {
        var bare = await Handle(Lambda.Get("Things", Lambda.Editor, null));
        var withItsLink = await Handle(Lambda.Get("Things", Lambda.Editor, """{"$query":"sample/Things"}"""));

        Assert.Equal((int)HttpStatusCode.OK, bare.StatusCode);
        Assert.Equal((int)HttpStatusCode.OK, withItsLink.StatusCode);
        Assert.Equal(HandleRestoreDue.Seed.Count, JsonNode.Parse(bare.Body)!["Things"]!.AsArray().Count);
    }

    [Fact]
    public async Task ACommandByGetIsNotAllowed()
    {
        var response = await Handle(Lambda.Get("DeleteThing", Lambda.Editor, """{"Id":"t1"}"""));

        Assert.Equal((int)HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("POST", response.Headers["allow"]);
        Assert.NotNull(_fakes.Things["t1"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"$command":"sample/DeleteThing","Id":"t1"}""")]
    public async Task ALinkByGetThatDoesntMakeALinkToTheQueryIsABadRequest(string link)
    {
        var response = await Handle(Lambda.Get("Things", Lambda.Editor, link));

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AFeatureTheUiBackendDoesntOfferIsNotFound()
    {
        var worker = await Handle(Lambda.Post("HandleRestoreDue", Lambda.Editor, null));
        var nothing = await Handle(Lambda.Post("Nothing", Lambda.Editor, null));
        var nothingByGet = await Handle(Lambda.Get("Nothing", Lambda.Editor, null));

        Assert.Equal((int)HttpStatusCode.NotFound, worker.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, nothing.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, nothingByGet.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"Id":null}""")]
    [InlineData("""{"$command":"sample/RenameThing","Id":"t1"}""")]
    [InlineData("""{"$command":"other/DeleteThing","Id":"t1"}""")]
    [InlineData("""{"$query":"sample/DeleteThing","Id":"t1"}""")]
    public async Task ArgumentsThatDontMakeALinkAreABadRequest(string body)
    {
        var response = await Handle(Lambda.Post("DeleteThing", Lambda.Editor, body));

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(_fakes.Things["t1"]);
    }

    [Fact]
    public async Task AnAnonymousCallerIsUnauthorizedAndAViewerForbiddenToDelete()
    {
        var anonymous = await Handle(Lambda.Post("DeleteThing", null, """{"Id":"t1"}"""));
        var viewer = await Handle(Lambda.Post("DeleteThing", Lambda.Viewer, """{"Id":"t1"}"""));

        Assert.Equal((int)HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal((int)HttpStatusCode.Forbidden, viewer.StatusCode);
        Assert.NotNull(_fakes.Things["t1"]);
    }

    [Fact]
    public async Task ABlankIdempotencyKeyIsABadRequest()
    {
        var response = await Handle(Lambda.Post("DeleteThing", Lambda.Editor, """{"Id":"t1"}""", " "));

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AResultIsJsonAndAnErrorIsAProblem()
    {
        var result = await Handle(Lambda.Post("Things", Lambda.Editor, null));
        var errors = new[]
        {
            await Handle(Lambda.Post("DeleteThing", Lambda.Editor, "not json")),
            await Handle(Lambda.Post("DeleteThing", null, """{"Id":"t1"}""")),
            await Handle(Lambda.Post("DeleteThing", Lambda.Viewer, """{"Id":"t1"}""")),
            await Handle(Lambda.Post("Nothing", Lambda.Editor, null)),
            await Handle(Lambda.Get("DeleteThing", Lambda.Editor, """{"Id":"t1"}"""))
        };

        Assert.Equal("application/json", result.Headers["content-type"]);
        Assert.Equal([400, 401, 403, 404, 405], errors.Select(error => error.StatusCode));
        Assert.All(errors, error =>
        {
            Assert.Equal("application/problem+json", error.Headers["content-type"]);
            var problem = JsonNode.Parse(error.Body)!;
            Assert.Equal("about:blank", (string?)problem["type"]);
            Assert.Equal(error.StatusCode, (int?)problem["status"]);
            Assert.False(string.IsNullOrEmpty((string?)problem["title"]));
            Assert.False(string.IsNullOrEmpty((string?)problem["detail"]));
        });
    }

    private Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request) =>
        _uiBackend.Handle(request, Lambda.Context());
}
