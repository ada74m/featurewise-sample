using Amazon.Lambda.Core;

namespace SampleService.Local;

// What Lambda tells a function about its invocation, as far as the functions use it: how long it has.
public class LocalLambdaContext : ILambdaContext
{
    public string AwsRequestId { get; } = Guid.NewGuid().ToString();
    public IClientContext ClientContext => null!;
    public string FunctionName => "local";
    public string FunctionVersion => "$LATEST";
    public ICognitoIdentity Identity => null!;
    public string InvokedFunctionArn => "local";
    public ILambdaLogger Logger { get; } = new ConsoleLogger();
    public string LogGroupName => "local";
    public string LogStreamName => "local";
    public int MemoryLimitInMB => 1024;
    public TimeSpan RemainingTime => TimeSpan.FromSeconds(30);

    private class ConsoleLogger : ILambdaLogger
    {
        public void Log(string message) => Console.Write(message);

        public void LogLine(string message) => Console.WriteLine(message);
    }
}
