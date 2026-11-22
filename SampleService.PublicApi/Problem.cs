namespace SampleService.PublicApi;

// An error response as RFC 9457 describes, without a type of its own, so its status says what it is.
public record Problem(string Title, int Status, string Detail)
{
    public string Type => "about:blank";
}
