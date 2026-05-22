using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Integration;

public class AuthTests : IntegrationTestBase
{
    public AuthTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = "admin@test.local",
            Password   = "TestPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TokenDto>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = "admin@test.local",
            Password   = "WrongPassword!"
        })
        .ShouldReturn(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownIdentifier_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = "nobody@nowhere.com",
            Password   = "anything"
        })
        .ShouldReturn(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokenPair()
    {
        // Login to get initial tokens
        var loginResp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = "admin@test.local",
            Password   = "TestPassword123!"
        });
        var tokens = await loginResp.Content.ReadFromJsonAsync<TokenDto>();

        // Refresh
        var refreshResp = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = tokens!.RefreshToken
        });

        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await refreshResp.Content.ReadFromJsonAsync<TokenDto>();
        newTokens!.AccessToken.Should().NotBe(tokens.AccessToken,
            "a new access token should be issued on refresh");
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = "this-is-not-a-valid-token"
        })
        .ShouldReturn(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        await Client.GetAsync("/api/actors")
            .ShouldReturn(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        var response = await AdminClient.GetAsync("/api/actors");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record TokenDto(string AccessToken, string RefreshToken, DateTime ExpiresAt);
}
