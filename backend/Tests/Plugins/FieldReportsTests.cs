using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class FieldReportsTests : IntegrationTestBase
{
    public FieldReportsTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    private async Task<(int ProjectId, int ActivityId, int AuthorId)> SeedPrerequisitesAsync()
    {
        var authorId   = await new ActorBuilder()
            .WithEmail($"author-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);
        var projectId  = await new ProjectBuilder().WithTitle("Report project").BuildAsync(AdminClient);
        var activityId = await new ActivityBuilder()
            .WithTitle("Report activity")
            .ForProject(projectId)
            .BuildAsync(AdminClient);

        return (projectId, activityId, authorId);
    }

    [Fact]
    public async Task PostFieldReport_WithValidData_Returns201()
    {
        var (_, activityId, authorId) = await SeedPrerequisitesAsync();

        var response = await AdminClient.PostAsJsonAsync("/api/fieldreports", new
        {
            Title          = "Visit report",
            Content        = "We visited the site and found everything in order.",
            AuthorActorId  = authorId,
            ActivityId     = activityId,
            ActivityType   = "Activity"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostFieldReport_WithoutTitle_Returns400()
    {
        var (_, activityId, authorId) = await SeedPrerequisitesAsync();

        await AdminClient.PostAsJsonAsync("/api/fieldreports", new
        {
            Title         = "",
            Content       = "Content without title",
            AuthorActorId = authorId
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetFieldReports_ReturnsList()
    {
        var (_, activityId, authorId) = await SeedPrerequisitesAsync();

        await AdminClient.PostAsJsonAsync("/api/fieldreports", new
        {
            Title         = "Report A",
            Content       = "Content A",
            AuthorActorId = authorId
        });

        var result = await AdminClient.GetJsonAsync("/api/fieldreports");
        result.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetFieldReport_ById_ReturnsReport()
    {
        var (_, _, authorId) = await SeedPrerequisitesAsync();

        var id = await AdminClient.PostAndGetIdAsync("/api/fieldreports", new
        {
            Title         = "Specific report",
            Content       = "Specific content",
            AuthorActorId = authorId
        });

        var report = await AdminClient.GetJsonAsync($"/api/fieldreports/{id}");
        report.GetProperty("id").GetInt32().Should().Be(id);
    }

    [Fact]
    public async Task GdprForget_AnonymisesAuthorActorId()
    {
        var (_, _, authorId) = await SeedPrerequisitesAsync();

        var reportId = await AdminClient.PostAndGetIdAsync("/api/fieldreports", new
        {
            Title         = "Report to forget",
            Content       = "Some content",
            AuthorActorId = authorId
        });

        // Forget the author
        await AdminClient.PostAsJsonAsync(
            $"/api/gdpr/forget/{authorId}?subjectType=Actor", new { });

        // Author ID on the report should now be the anonymous sentinel (-1)
        var report = await AdminClient.GetJsonAsync($"/api/fieldreports/{reportId}");
        report.GetProperty("authorActorId").GetInt32().Should().Be(-1,
            "AuthorActorId must be replaced with AnonymousActorId after GDPR forget");
    }

    [Fact]
    public async Task PutFieldReport_UpdatesContent()
    {
        var (_, _, authorId) = await SeedPrerequisitesAsync();

        var id = await AdminClient.PostAndGetIdAsync("/api/fieldreports", new
        {
            Title         = "Original title",
            Content       = "Original content",
            AuthorActorId = authorId
        });

        var updateResponse = await AdminClient.PutAsJsonAsync($"/api/fieldreports/{id}", new
        {
            Title         = "Updated title",
            Content       = "Updated content",
            AuthorActorId = authorId
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = await AdminClient.GetJsonAsync($"/api/fieldreports/{id}");
        updated.GetProperty("title").GetString().Should().Be("Updated title");
    }

    [Fact]
    public async Task DeleteFieldReport_Returns204()
    {
        var (_, _, authorId) = await SeedPrerequisitesAsync();

        var id = await AdminClient.PostAndGetIdAsync("/api/fieldreports", new
        {
            Title         = "Delete me",
            Content       = "Content",
            AuthorActorId = authorId
        });

        (await AdminClient.DeleteAsync($"/api/fieldreports/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AdminClient.GetAsync($"/api/fieldreports/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
