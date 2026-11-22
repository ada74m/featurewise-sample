using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SampleService.Local;

// The claims of a bearer token, read without verifying it, as API Gateway's JWT authorizer would pass
// them on after verifying it: each as a string, with arrays flattened to "[a b]". Only the local host
// does this, so a developer can be anyone with a token they make themselves, such as one with the
// claims {"username":"ann","cognito:groups":["editor"]}.
public static class UnsignedJwt
{
    public static Dictionary<string, string>? Claims(string? authorization)
    {
        const string bearer = "Bearer ";
        if (authorization is null || !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = authorization[bearer.Length..].Trim().Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            return (JsonNode.Parse(Base64UrlDecode(parts[1])) as JsonObject)?
                .Where(claim => claim.Value is not null)
                .ToDictionary(claim => claim.Key, claim => Flatten(claim.Value!));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }

    private static string Flatten(JsonNode value) =>
        value switch
        {
            JsonArray array => $"[{string.Join(' ', array.Select(item => item is null ? "" : Flatten(item)))}]",
            JsonValue scalar when scalar.TryGetValue<string>(out var text) => text,
            _ => value.ToJsonString()
        };

    // Tokens use base64url without padding; tolerate plain base64 too, which is easier to make by hand.
    private static string Base64UrlDecode(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
