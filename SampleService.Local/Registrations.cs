using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SampleService.Local;

// The core's facilities in memory, as every function the local host runs registers them. The functions
// share one store, as they share one table in AWS.
public static class Registrations
{
    public static Action<IServiceCollection> Over(InMemoryThings things) => services =>
    {
        services.AddLogging(logging => logging.AddSimpleConsole(console => console.IncludeScopes = true));
        AddFacilities(services, things);
    };

    // The facilities alone, without the console logging, as the tests register them.
    public static void AddFacilities(IServiceCollection services, InMemoryThings things)
    {
        services.AddSingleton(things);

        // Each invocation has its own unit of work, which its facilities stage their writes in.
        services.AddScoped<InMemoryChanges>();
        services.AddScoped<IChanges>(provider => provider.GetRequiredService<InMemoryChanges>());
        services.AddScoped<IThingsPersistence, InMemoryThingsPersistence>();
    }
}
