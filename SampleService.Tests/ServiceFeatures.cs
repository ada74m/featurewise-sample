using Featurewise;

namespace SampleService.Tests;

// Builds a standalone Features over the fakes, for testing core features without an entrypoint.
public static class ServiceFeatures
{
    public static Features Over(Fakes fakes, Action<Features> register)
    {
        var features = new Features(Service.Id);
        register(features);
        return features.RegisterDependencies(fakes.Register).Build();
    }
}
