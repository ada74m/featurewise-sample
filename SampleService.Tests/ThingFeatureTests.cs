using Featurewise;

namespace SampleService.Tests;

public class ThingFeatureTests
{
    private readonly Fakes _fakes = new Fakes().WithSeed();

    private Features Features() => ServiceFeatures.Over(_fakes, f => f
        .RegisterFeature<Things, AnySignedInCaller<Things.Args>>()
        .RegisterFeature<DeleteThing, DeleteThing.Rule>()
        .RegisterFeature<RenameThing, AnySignedInCaller<RenameThing.Args>>()
        .RegisterFeature<HandleRestoreDue, AnySystemCaller<HandleRestoreDue.Args>>());

    [Fact]
    public async Task ThingsListsEveryThingWithLinksToDeleteAndRenameItForAnEditor()
    {
        await using var features = Features();

        var result = await features.Invoke(new Things.Args(), Callers.Editor);

        Assert.Equal(HandleRestoreDue.Seed, result.Things.Select(entry => entry.Thing));
        var teapot = result.Things[0];
        Assert.Equal(new DeleteThing.Args("t1"), teapot.LinkToDelete);
        Assert.Equal(new RenameThing.Args("t1", null), teapot.LinkToRename);
    }

    [Fact]
    public async Task ThingsOffersSomeoneWhoIsntAnEditorNoLinkToDelete()
    {
        await using var features = Features();

        var result = await features.Invoke(new Things.Args(), Callers.Viewer);

        Assert.All(result.Things, entry => Assert.Null(entry.LinkToDelete));
        Assert.All(result.Things, entry => Assert.NotNull(entry.LinkToRename));
    }

    [Fact]
    public async Task DeletingAThingLeadsBackToThings()
    {
        await using var features = Features();

        var result = await features.Invoke(new DeleteThing.Args("t1"), Callers.Editor);

        Assert.Equal(new DeleteThing.Result.Deleted(new Things.Args()), result);
        Assert.Null(_fakes.Things["t1"]);
    }

    [Fact]
    public async Task DeletingAThingThatIsntThereSaysSo()
    {
        await using var features = Features();

        Assert.Equal(new DeleteThing.Result.NotFound("t9"), await features.Invoke(new DeleteThing.Args("t9"), Callers.Editor));
    }

    [Fact]
    public async Task OnlyAnEditorMayDelete()
    {
        await using var features = Features();

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(new DeleteThing.Args("t1"), Callers.Viewer));
        Assert.NotNull(_fakes.Things["t1"]);
    }

    [Fact]
    public async Task RenamingAThingSavesItsNewName()
    {
        await using var features = Features();

        var result = await features.Invoke(new RenameThing.Args("t1", " Kettle "), Callers.Viewer);

        Assert.Equal(new RenameThing.Result.Renamed(new Thing("t1", "Kettle"), new Things.Args()), result);
        Assert.Equal("Kettle", _fakes.Things["t1"]!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task RenamingWithoutANewNameAsksForOne(string? newName)
    {
        await using var features = Features();

        var result = await features.Invoke(new RenameThing.Args("t1", newName), Callers.Viewer);

        Assert.Equal(new RenameThing.Result.NeedsNewName(new Thing("t1", "Teapot"), new RenameThing.Args("t1", newName)), result);
        Assert.Equal("Teapot", _fakes.Things["t1"]!.Name);
    }

    [Fact]
    public async Task RenamingAThingThatIsntThereSaysSo()
    {
        await using var features = Features();

        Assert.Equal(new RenameThing.Result.NotFound("t9"), await features.Invoke(new RenameThing.Args("t9", "Kettle"), Callers.Viewer));
    }

    [Fact]
    public async Task RestoringPutsTheThingsBackAsTheyStarted()
    {
        await using var features = Features();
        await features.Invoke(new DeleteThing.Args("t1"), Callers.Editor);
        await features.Invoke(new RenameThing.Args("t2", "Kettle"), Callers.Editor);
        _fakes.Things.Put(new Thing("t9", "Stray"));

        await features.Invoke(new HandleRestoreDue.Args(), Callers.Job);

        Assert.Equal(HandleRestoreDue.Seed, _fakes.Things.All());
    }

    [Fact]
    public async Task OnlyAJobMayRestore()
    {
        await using var features = Features();

        await Assert.ThrowsAsync<InvocationDeniedException>(() => features.Invoke(new HandleRestoreDue.Args(), Callers.Editor));
    }
}
