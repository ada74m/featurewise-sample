using Featurewise;

namespace SampleService.ScheduledWorker;

// What a schedule sends: the link to invoke, and the schedule and execution it comes from, which
// EventBridge Scheduler fills in from <aws.scheduler.schedule-arn> and <aws.scheduler.execution-id>.
// The execution id stays the same when Scheduler retries, so it serves as the idempotency key.
public record ScheduledInvocation(string Schedule, string? Execution, Link Link)
{
    // The schedule's name, the last part of its ARN, which names the job's caller.
    public string ScheduleName() => Schedule[(Schedule.LastIndexOf('/') + 1)..];
}
