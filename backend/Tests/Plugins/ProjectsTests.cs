using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class ProjectsTests : IntegrationTestBase
{
    public ProjectsTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PostProject_WithValidData_Returns201()
    {
        var response = await AdminClient.PostAsJsonAsync("/api/projects", new
        {
            Title       = "New Project",
            Description = "Project description"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostProject_WithoutTitle_Returns400()
    {
        await AdminClient.PostAsJsonAsync("/api/projects", new
        {
            Title       = "",
            Description = "No title"
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProjects_ReturnsAllProjectsForTenant()
    {
        await new ProjectBuilder().WithTitle("Project Alpha").BuildAsync(AdminClient);
        await new ProjectBuilder().WithTitle("Project Beta").BuildAsync(AdminClient);

        var result = await AdminClient.GetJsonAsync("/api/projects");
        result.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetProject_ById_ReturnsCorrectProject()
    {
        var id = await new ProjectBuilder()
            .WithTitle("Specific Project")
            .WithDescription("Specific description")
            .BuildAsync(AdminClient);

        var project = await AdminClient.GetJsonAsync($"/api/projects/{id}");

        // Unwrap data envelope if present
        var data = project.TryGetProperty("data", out var d) ? d : project;
        var inner = data.TryGetProperty("data", out var inner2) ? inner2 : data;

        inner.GetProperty("id").GetInt32().Should().Be(id);
    }

    [Fact]
    public async Task GetProject_NonExistent_Returns404()
    {
        var response = await AdminClient.GetAsync("/api/projects/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutProject_UpdatesTitle()
    {
        var id = await new ProjectBuilder().WithTitle("Old Title").BuildAsync(AdminClient);

        var updateResponse = await AdminClient.PutAsJsonAsync($"/api/projects/{id}", new
        {
            Id          = id,
            Title       = "New Title",
            Description = "Updated"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = await AdminClient.GetJsonAsync($"/api/projects/{id}");
        var data    = updated.TryGetProperty("data", out var d) ? d : updated;
        var inner   = data.TryGetProperty("data", out var i) ? i : data;
        inner.GetProperty("title").GetString().Should().Be("New Title");
    }

    [Fact]
    public async Task DeleteProject_Returns204()
    {
        var id = await new ProjectBuilder().WithTitle("To Delete").BuildAsync(AdminClient);

        var deleteResponse = await AdminClient.DeleteAsync($"/api/projects/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await AdminClient.GetAsync($"/api/projects/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_AddsActorToProject()
    {
        var projectId = await new ProjectBuilder().WithTitle("Member project").BuildAsync(AdminClient);
        var actorId   = await new ActorBuilder()
            .WithEmail($"member-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        var response = await AdminClient.PostAsync(
            $"/api/projects/{projectId}/members/{actorId}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddMember_Duplicate_Returns409()
    {
        var projectId = await new ProjectBuilder().WithTitle("Dup member project").BuildAsync(AdminClient);
        var actorId   = await new ActorBuilder()
            .WithEmail($"dupmember-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        await AdminClient.PostAsync($"/api/projects/{projectId}/members/{actorId}", null);

        // Add same member again
        var second = await AdminClient.PostAsync(
            $"/api/projects/{projectId}/members/{actorId}", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveMember_RemovesActorFromProject()
    {
        var projectId = await new ProjectBuilder().WithTitle("Remove member project").BuildAsync(AdminClient);
        var actorId   = await new ActorBuilder()
            .WithEmail($"removemember-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        await AdminClient.PostAsync($"/api/projects/{projectId}/members/{actorId}", null);

        var removeResponse = await AdminClient
            .DeleteAsync($"/api/projects/{projectId}/members/{actorId}");

        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ProjectSchema_ExistsInSchemaEndpoint()
    {
        var schema = await AdminClient.GetJsonAsync("/api/schema/Project");
        schema.GetProperty("entityType").GetString().Should().Be("Project");
        schema.GetProperty("apiBasePath").GetString().Should().Be("/api/projects");
    }
}
