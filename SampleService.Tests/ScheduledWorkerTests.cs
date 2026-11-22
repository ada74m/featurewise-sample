using System.Text;
using System.Text.Json;
using SampleService.ScheduledWorker;

namespace SampleService.Tests;

public class ScheduledWorkerTests
{
    private const string ScheduleArn = "arn:aws:scheduler:eu-west-2:123456789012:schedule/default/restore";

    private readonly Fakes _fakes = new();
    private readonly Function _worker;

    public ScheduledWorkerTests() => _worker = new Function(_fakes.Register);

    [Fact]
    public async Task AScheduleRunsTheJobItsInputNames()
    {
        await Handle($$$"""{"Schedule":"{{{ScheduleArn}}}","Execution":"x1","Link":{"$worker":"sample/HandleRestoreDue"}}""");

        Assert.Equal(HandleRestoreDue.Seed.Count, _fakes.Things.Count);
    }

    [Theory]
    [InlineData("""{"Schedule":"s","Link":{"$command":"sample/DeleteThing","Id":"t1"}}""")]
    [InlineData("""{"Schedule":"s"}""")]
    [InlineData("""{"Link":{"$worker":"sample/HandleRestoreDue"}}""")]
    public async Task AnInputThatDoesntNameOneOfTheWorkersJobsFails(string input)
    {
        await Assert.ThrowsAsync<JsonException>(() => Handle(input));
        Assert.Equal(0, _fakes.Things.Count);
    }

    private Task Handle(string input) =>
        _worker.Handle(new MemoryStream(Encoding.UTF8.GetBytes(input)), Lambda.Context());
}
