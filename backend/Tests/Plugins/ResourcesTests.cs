using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class ResourcesTests : IntegrationTestBase
{
    public ResourcesTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task AddLink_WithValidUrl_Returns201()
    {
        var projectId = await new ProjectBuilder().WithTitle("Resource project").BuildAsync(AdminClient);

        var response = await AdminClient.PostAsJsonAsync("/api/resources/link", new
        {
            Title       = "Useful link",
            ExternalUrl = "https://example.com",
            TargetId    = projectId,
            TargetType  = "Project"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddLink_WithInvalidUrl_Returns400()
    {
        var projectId = await new ProjectBuilder().WithTitle("Bad URL project").BuildAsync(AdminClient);

        await AdminClient.PostAsJsonAsync("/api/resources/link", new
        {
            Title       = "Bad link",
            ExternalUrl = "not-a-url",
            TargetId    = projectId,
            TargetType  = "Project"
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetResources_ForTarget_ReturnsCorrectResources()
    {
        var projectId = await new ProjectBuilder().WithTitle("Listed resources project").BuildAsync(AdminClient);

        await AdminClient.PostAsJsonAsync("/api/resources/link", new
        {
            Title       = "Link 1",
            ExternalUrl = "https://link1.example.com",
            TargetId    = projectId,
            TargetType  = "Project"
        });

        var resources = await AdminClient.GetJsonAsync(
            $"/api/resources?targetType=Project&targetId={projectId}");

        resources.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteResource_Returns204()
    {
        var projectId  = await new ProjectBuilder().WithTitle("Delete resource project").BuildAsync(AdminClient);
        var resourceId = await AdminClient.PostAndGetIdAsync("/api/resources/link", new
        {
            Title       = "To delete",
            ExternalUrl = "https://delete-me.example.com",
            TargetId    = projectId,
            TargetType  = "Project"
        });

        (await AdminClient.DeleteAsync($"/api/resources/{resourceId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UploadEndpoint_WithoutFile_Returns400()
    {
        // POST /api/resources/upload with no file — multipart form with empty content
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Project"),   "targetType");
        form.Add(new StringContent("1"),         "targetId");
        form.Add(new StringContent("No file"),   "title");
        // No file part added intentionally

        var response = await AdminClient.PostAsync("/api/resources/upload", form);

        // 400 or 415 — either means "no valid file"
        ((int)response.StatusCode).Should().BeOneOf(400, 415);
    }

    [Fact]
    public async Task GetResources_RequiresAuthentication()
    {
        await Client.GetAsync("/api/resources?targetType=Project&targetId=1")
            .ShouldReturn(HttpStatusCode.Unauthorized);
    }
}
