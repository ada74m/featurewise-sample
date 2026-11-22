using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SampleService.Aws;

// The core's facilities on AWS, as every function registers them, configured by its environment.
public static class Registrations
{
    public static void Register(IServiceCollection services)
    {
        // Lambda's logger writes each entry as it's logged, so nothing is lost when the function freezes.
        services.AddLogging(logging => logging.AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true }));

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
        // Each invocation has its own unit of work, which its facilities stage their writes in.
        services.AddScoped<DynamoChanges>();
        services.AddScoped<IChanges>(provider => provider.GetRequiredService<DynamoChanges>());
        services.AddScoped<IThingsPersistence>(provider => new DynamoThingsPersistence(
            provider.GetRequiredService<IAmazonDynamoDB>(), provider.GetRequiredService<DynamoChanges>(),
            Setting("THINGS_TABLE")));
    }

    private static string Setting(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"the environment variable {name} isn't set");
}
