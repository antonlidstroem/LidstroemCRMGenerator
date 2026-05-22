using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class ActorsTests : IntegrationTestBase
{
    public ActorsTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PostActor_WithValidData_Returns201()
    {
        var response = await AdminClient.PostAsJsonAsync("/api/actors", new
        {
            DisplayName = "Jane Doe",
            Email       = $"jane-{Guid.NewGuid():N}@test.local",
            PhoneNumber = "070-123 45 67"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostActor_WithoutDisplayName_Returns400()
    {
        await AdminClient.PostAsJsonAsync("/api/actors", new
        {
            DisplayName = "",
            Email       = $"nodisplay-{Guid.NewGuid():N}@test.local"
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostActor_WithoutEmail_Returns400()
    {
        await AdminClient.PostAsJsonAsync("/api/actors", new
        {
            DisplayName = "No Email",
            Email       = ""
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetActor_ById_ReturnsCorrectActor()
    {
        var email = $"getbyid-{Guid.NewGuid():N}@test.local";
        var id    = await new ActorBuilder()
            .WithName("Get By Id Actor")
            .WithEmail(email)
            .BuildAsync(AdminClient);

        var result = await AdminClient.GetJsonAsync($"/api/actors/{id}");

        // Response may be wrapped in { data: { data: actor } } envelope
        var data  = result.TryGetProperty("data", out var d) ? d : result;
        var inner = data.TryGetProperty("data",  out var i) ? i : data;
        inner.GetProperty("id").GetInt32().Should().Be(id);
    }

    [Fact]
    public async Task GetActor_NonExistent_Returns404()
    {
        var response = await AdminClient.GetAsync("/api/actors/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutActor_UpdatesDisplayName()
    {
        var id = await new ActorBuilder()
            .WithName("Old Name")
            .WithEmail($"update-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        var updateResp = await AdminClient.PutAsJsonAsync($"/api/actors/{id}", new
        {
            Id          = id,
            DisplayName = "New Name",
            Email       = $"update-{id}@test.local"
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = await AdminClient.GetJsonAsync($"/api/actors/{id}");
        var data    = updated.TryGetProperty("data", out var d) ? d : updated;
        var inner   = data.TryGetProperty("data",   out var i) ? i : data;
        inner.GetProperty("displayName").GetString().Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteActor_Returns204()
    {
        var id = await new ActorBuilder()
            .WithEmail($"delete-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        (await AdminClient.DeleteAsync($"/api/actors/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AdminClient.GetAsync($"/api/actors/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetActors_RequiresAuthentication()
    {
        await Client.GetAsync("/api/actors")
            .ShouldReturn(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActorSchema_ExistsInSchemaEndpoint()
    {
        var schema = await AdminClient.GetJsonAsync("/api/schema/Actor");
        schema.GetProperty("entityType").GetString().Should().Be("Actor");
        schema.GetProperty("navOrder").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task ActorDetail_HasExtensionsEnvelope()
    {
        var id = await new ActorBuilder()
            .WithEmail($"extenv-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        var result = await AdminClient.GetJsonAsync($"/api/actors/{id}");

        result.TryGetProperty("extensions", out _).Should().BeTrue(
            "actor detail should always return an extensions envelope, even when empty");
    }
}
