namespace SampleService.PublicApi;

// A Thing as the public API shows it: its own shape, without the links the UI backend offers.
public record ThingView(string Id, string Name)
{
    public static ThingView From(Thing thing) => new(thing.Id, thing.Name);
}
