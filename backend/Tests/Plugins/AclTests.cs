using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class AclTests : IntegrationTestBase
{
    public AclTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GrantAccess_ValidEntry_Returns204()
    {
        var granteeId = await new ActorBuilder()
            .WithEmail($"grantee-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        var response = await AdminClient.PostAsJsonAsync("/api/acl/grant", new
        {
            ResourceId       = 1,
            ResourceType     = "Project",
            GrantedToActorId = granteeId,
            Action           = "Read",
            ExpiresAt        = (DateTime?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetGrants_ForResource_ReturnsActiveGrants()
    {
        var granteeId = await new ActorBuilder()
            .WithEmail($"grantee2-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        await AdminClient.PostAsJsonAsync("/api/acl/grant", new
        {
            ResourceId       = 42,
            ResourceType     = "Project",
            GrantedToActorId = granteeId,
            Action           = "Write"
        });

        var grants = await AdminClient.GetJsonAsync("/api/acl/resource/Project/42");
        grants.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RevokeAccess_RemovesGrant()
    {
        var granteeId = await new ActorBuilder()
            .WithEmail($"revokee-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        // Grant
        await AdminClient.PostAsJsonAsync("/api/acl/grant", new
        {
            ResourceId       = 99,
            ResourceType     = "Project",
            GrantedToActorId = granteeId,
            Action           = "Delete"
        });

        // Revoke
        var revokeResp = await AdminClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/acl/revoke")
            {
                Content = JsonContent.Create(new
                {
                    ResourceId       = 99,
                    ResourceType     = "Project",
                    GrantedToActorId = granteeId,
                    Action           = "Delete"
                })
            });

        revokeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMyGrants_RequiresAuthentication()
    {
        await Client.GetAsync("/api/acl/mine")
            .ShouldReturn(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyGrants_AuthenticatedUser_ReturnsOk()
    {
        var response = await AdminClient.GetAsync("/api/acl/mine");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
