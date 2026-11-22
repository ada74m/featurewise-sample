namespace SampleService;

// Who this service is in the estate. Every link it writes names it, so the UI shell sends each link back
// to the service it came from. Links outlive deployments, so the id must not change.
public static class Service
{
    public const string Id = "sample";
}
