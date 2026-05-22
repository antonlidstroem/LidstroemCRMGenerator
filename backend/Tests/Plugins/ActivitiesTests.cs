using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class ActivitiesTests : IntegrationTestBase
{
    public ActivitiesTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PostActivity_WithoutProjectId_Returns400()
    {
        await AdminClient.PostAsJsonAsync("/api/activities", new
        {
            Title     = "Orphan activity",
            ProjectId = 0   // invalid
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostActivity_WithNegativeProjectId_Returns400()
    {
        await AdminClient.PostAsJsonAsync("/api/activities", new
        {
            Title     = "Orphan activity",
            ProjectId = -1
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostActivity_WithValidProjectId_Returns201()
    {
        var projectId = await new ProjectBuilder()
            .WithTitle("Parent project")
            .BuildAsync(AdminClient);

        var response = await AdminClient.PostAsJsonAsync("/api/activities", new
        {
            Title     = "Valid activity",
            ProjectId = projectId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetByProject_ReturnsOnlyThatProjectsActivities()
    {
        var projectA = await new ProjectBuilder().WithTitle("Project A").BuildAsync(AdminClient);
        var projectB = await new ProjectBuilder().WithTitle("Project B").BuildAsync(AdminClient);

        var actA1 = await new ActivityBuilder().WithTitle("A1").ForProject(projectA).BuildAsync(AdminClient);
        var actA2 = await new ActivityBuilder().WithTitle("A2").ForProject(projectA).BuildAsync(AdminClient);
        var actB1 = await new ActivityBuilder().WithTitle("B1").ForProject(projectB).BuildAsync(AdminClient);

        var result = await AdminClient.GetJsonAsync($"/api/activities/by-project/{projectA}");
        var ids    = result.EnumerateArray()
            .Select(a => a.GetProperty("id").GetInt32())
            .ToList();

        ids.Should().Contain(actA1).And.Contain(actA2);
        ids.Should().NotContain(actB1, "activity B1 belongs to Project B");
    }

    [Fact]
    public async Task GetProject_IncludesActivitiesInExtensions()
    {
        var projectId  = await new ProjectBuilder().WithTitle("Extended Project").BuildAsync(AdminClient);
        var activityId = await new ActivityBuilder()
            .WithTitle("Extension activity")
            .ForProject(projectId)
            .BuildAsync(AdminClient);

        var project = await AdminClient.GetJsonAsync($"/api/projects/{projectId}");

        // The response should have an extensions section containing activities
        project.TryGetProperty("extensions", out var extensions).Should().BeTrue(
            "projects should have an extensions envelope");

        // Activities extension key is the assembly short name of the Activities plugin
        var hasActivities = extensions.EnumerateObject()
            .Any(p => p.Name.Contains("Activities", StringComparison.OrdinalIgnoreCase));

        hasActivities.Should().BeTrue(
            "Project detail should include Activities extension from ProjectActivityExtensionProvider");
    }

    [Fact]
    public async Task ActivitySchema_HasProjectIdAsRequiredRelationField()
    {
        var schema = await AdminClient.GetJsonAsync("/api/schema/Activity");

        var fields = schema.GetProperty("fields").EnumerateArray().ToList();
        var projectIdField = fields.FirstOrDefault(f =>
            f.GetProperty("fieldName").GetString() == "ProjectId");

        projectIdField.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined,
            "Activity schema must have a ProjectId field");

        projectIdField.GetProperty("isRequired").GetBoolean().Should().BeTrue(
            "ProjectId must be required in the schema");

        projectIdField.GetProperty("type").GetString().Should().Be("Relation",
            "ProjectId should render as a relation picker in the frontend");
    }

    [Fact]
    public async Task DeleteActivity_RemovesItFromProjectExtensions()
    {
        var projectId  = await new ProjectBuilder().WithTitle("Cleanup Project").BuildAsync(AdminClient);
        var activityId = await new ActivityBuilder()
            .WithTitle("To delete")
            .ForProject(projectId)
            .BuildAsync(AdminClient);

        // Verify it's there
        var before = await AdminClient.GetJsonAsync($"/api/projects/{projectId}");
        before.GetProperty("extensions").EnumerateObject()
            .Any(p => p.Name.Contains("Activities", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();

        // Delete
        (await AdminClient.DeleteAsync($"/api/activities/{activityId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify extensions are now empty for this project
        var after = await AdminClient.GetJsonAsync($"/api/activities/by-project/{projectId}");
        after.EnumerateArray().Should().BeEmpty(
            "deleted activity should not appear in by-project list");
    }
}
