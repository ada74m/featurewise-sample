namespace SampleService.UiBackend;

// An error response as RFC 9457 describes, without a type of its own, so its status says what it is. The
// UI shell tells it from a result by its content type, application/problem+json.
public record Problem(string Title, int Status, string Detail)
{
    public string Type => "about:blank";
}
