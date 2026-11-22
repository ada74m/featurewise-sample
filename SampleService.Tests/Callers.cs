using System.Security.Claims;
using Featurewise;

namespace SampleService.Tests;

// Callers for invoking features directly: Ann, an editor, Bob, who isn't one, and a scheduled job.
public static class Callers
{
    public static ClaimsPrincipal Editor { get; } = Person("ann", "editor");

    public static ClaimsPrincipal Viewer { get; } = Person("bob");

    public static ClaimsPrincipal Job { get; } = SystemCaller.For("test-job");

    private static ClaimsPrincipal Person(string name, params string[] roles) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), .. roles.Select(role => new Claim(ClaimTypes.Role, role))],
            authenticationType: "test"));
}
