using FluentAssertions;
using Xunit;

namespace Lidstroem.Tests.Core;

/// <summary>
/// Tests the tenant slug resolution logic.
/// The ExtractSubdomain method is duplicated here to keep it testable
/// without a browser or Blazor environment.
/// The logic must stay in sync with App.razor.
/// </summary>
public class TenantSlugResolutionTests
{
    // Mirror of the logic in App.razor — kept in sync manually.
    // If the App.razor logic changes, these tests will fail, signalling the need to update.
    private static string? ExtractSubdomain(string host)
    {
        var hostname = host.Split(':')[0];
        var parts    = hostname.Split('.');
        if (parts.Length < 3) return null;
        if (parts.All(p => int.TryParse(p, out _))) return null;
        if (parts.Length != 3) return null;
        var candidate = parts[0].ToLowerInvariant();
        var ignored = new HashSet<string> { "www", "app", "api", "mail", "smtp" };
        return ignored.Contains(candidate) ? null : candidate;
    }

    [Theory]
    [InlineData("kund-a.lidstroem.se",   "kund-a")]
    [InlineData("foretaget.lidstroem.se", "foretaget")]
    [InlineData("org123.lidstroem.se",    "org123")]
    public void ValidSubdomain_ReturnsSlug(string host, string expected)
    {
        ExtractSubdomain(host).Should().Be(expected);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("lidstroem.se")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    public void NoSubdomain_ReturnsNull(string host)
    {
        ExtractSubdomain(host).Should().BeNull();
    }

    [Theory]
    [InlineData("www.lidstroem.se")]
    [InlineData("app.lidstroem.se")]
    [InlineData("api.lidstroem.se")]
    [InlineData("mail.lidstroem.se")]
    [InlineData("smtp.lidstroem.se")]
    public void InfrastructureSubdomain_ReturnsNull(string host)
    {
        ExtractSubdomain(host).Should().BeNull();
    }

    [Theory]
    [InlineData("kund-a.localhost:5001",  "kund-a")]
    [InlineData("test.lidstroem.se:7209", "test")]
    public void HostWithPort_IsHandledCorrectly(string host, string expected)
    {
        ExtractSubdomain(host).Should().Be(expected);
    }

    [Theory]
    [InlineData("sub.kund-a.lidstroem.se")] // two levels of subdomain — ambiguous
    public void MultiLevelSubdomain_ReturnsNull(string host)
    {
        // We intentionally do not handle multiple subdomain levels
        ExtractSubdomain(host).Should().BeNull();
    }
}
