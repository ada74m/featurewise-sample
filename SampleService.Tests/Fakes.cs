using Microsoft.Extensions.DependencyInjection;
using SampleService.Local;

namespace SampleService.Tests;

// The local host's store, registered in place of AWS's, which tests seed and inspect.
public class Fakes
{
    public InMemoryThings Things { get; } = new();

    public void Register(IServiceCollection services) => Registrations.AddFacilities(services, Things);

    // Seeds the store with HandleRestoreDue's Things.
    public Fakes WithSeed()
    {
        foreach (var thing in HandleRestoreDue.Seed)
            Things.Put(thing);
        return this;
    }
}
