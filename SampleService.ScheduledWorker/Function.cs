using System.Text.Json;
using Amazon.Lambda.Core;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

namespace SampleService.ScheduledWorker;

// Runs the service's scheduled jobs. Each EventBridge schedule's input names the job as a link, so one
// function serves every schedule.
public class Function
{
    private readonly Features _features = new(Service.Id);
    private readonly JsonSerializerOptions _json;

    public Function() : this(Aws.Registrations.Register)
    {
    }

    public Function(Action<IServiceCollection> registerDependencies)
    {
        _features
            .RegisterFeature<HandleRestoreDue, AnySystemCaller<HandleRestoreDue.Args>>()
            .RegisterDependencies(registerDependencies)
            .Build();

        _json = _features.ConfigureJson(new JsonSerializerOptions());
    }

    // Reads the input itself, as a stream, because Lambda's serializer can't be given options that know
    // the registered features.
    public async Task Handle(Stream input, ILambdaContext context)
    {
        var scheduled = await JsonSerializer.DeserializeAsync<ScheduledInvocation>(input, _json);
        if (scheduled?.Link is null || string.IsNullOrWhiteSpace(scheduled.Schedule))
            throw new JsonException("the input must have a Schedule and a Link");

        using var timeout = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1));
        await _features.Invoke(
            scheduled.Link, SystemCaller.For(scheduled.ScheduleName()), scheduled.Execution, timeout.Token);
    }
}
