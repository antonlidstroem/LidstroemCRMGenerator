using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class InvitationsTests : IntegrationTestBase
{
    public InvitationsTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SendInvitation_WithValidEmail_Returns201()
    {
        var response = await AdminClient.PostAsJsonAsync("/api/invitations", new
        {
            Email    = $"invite-{Guid.NewGuid():N}@test.local",
            RoleName = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ValidateToken_WithValidToken_ReturnsEmailAndExpiry()
    {
        var email = $"validate-{Guid.NewGuid():N}@test.local";

        var createResp = await AdminClient.PostAsJsonAsync("/api/invitations", new
        {
            Email    = email,
            RoleName = (string?)null
        });
        createResp.EnsureSuccessStatusCode();

        var invitation = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var token      = invitation.GetProperty("token").GetString();

        token.Should().NotBeNullOrEmpty();

        var validResp = await Client.GetAsync($"/api/invitations/validate/{token}");
        validResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await validResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(email);
        body.TryGetProperty("expiresAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateToken_WithFakeToken_Returns400()
    {
        var response = await Client.GetAsync("/api/invitations/validate/this-token-does-not-exist");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcceptInvitation_CreatesActorAndCredentials()
    {
        var email = $"accept-{Guid.NewGuid():N}@test.local";

        // Send invitation
        var sendResp = await AdminClient.PostAsJsonAsync("/api/invitations", new
        {
            Email    = email,
            RoleName = (string?)null
        });
        var invitation = await sendResp.Content.ReadFromJsonAsync<JsonElement>();
        var token      = invitation.GetProperty("token").GetString()!;

        // Accept it
        var acceptResp = await Client.PostAsJsonAsync("/api/invitations/accept", new
        {
            Token       = token,
            DisplayName = "New User",
            Password    = "SecurePass123!"
        });

        acceptResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body    = await acceptResp.Content.ReadFromJsonAsync<JsonElement>();
        var actorId = body.GetProperty("actorId").GetInt32();
        actorId.Should().BeGreaterThan(0);

        // Verify they can now log in
        var loginResp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = email,
            Password   = "SecurePass123!"
        });

        loginResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "invited user should be able to log in with the password they set on accept");
    }

    [Fact]
    public async Task AcceptInvitation_SameToken_Twice_Returns400()
    {
        var email = $"twice-{Guid.NewGuid():N}@test.local";

        var sendResp = await AdminClient.PostAsJsonAsync("/api/invitations", new
        {
            Email    = email,
            RoleName = (string?)null
        });
        var invitation = await sendResp.Content.ReadFromJsonAsync<JsonElement>();
        var token      = invitation.GetProperty("token").GetString()!;

        // Accept once
        await Client.PostAsJsonAsync("/api/invitations/accept", new
        {
            Token       = token,
            DisplayName = "User One",
            Password    = "Pass1234!"
        });

        // Accept again — should fail
        var secondAccept = await Client.PostAsJsonAsync("/api/invitations/accept", new
        {
            Token       = token,
            DisplayName = "User Two",
            Password    = "Pass5678!"
        });

        secondAccept.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an already-accepted invitation token cannot be used again");
    }

    [Fact]
    public async Task RevokeInvitation_SetsPendingToRevoked()
    {
        var email = $"revoke-{Guid.NewGuid():N}@test.local";

        var sendResp   = await AdminClient.PostAsJsonAsync("/api/invitations", new { Email = email, RoleName = (string?)null });
        var invitation = await sendResp.Content.ReadFromJsonAsync<JsonElement>();
        var id         = invitation.GetProperty("id").GetInt32();

        var revokeResp = await AdminClient.DeleteAsync($"/api/invitations/{id}");
        revokeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Token should now be invalid
        var token       = invitation.GetProperty("token").GetString()!;
        var validateResp = await Client.GetAsync($"/api/invitations/validate/{token}");
        validateResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "revoked invitation token should be rejected");
    }
}
