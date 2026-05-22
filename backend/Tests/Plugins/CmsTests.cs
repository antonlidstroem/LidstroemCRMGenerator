using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class CmsTests : IntegrationTestBase
{
    public CmsTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetPages_RequiresAuthentication()
    {
        await Client.GetAsync("/api/cms/pages")
            .ShouldReturn(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPages_AuthenticatedUser_ReturnsOk()
    {
        var response = await AdminClient.GetAsync("/api/cms/pages");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatePage_WithValidData_Returns201()
    {
        var response = await AdminClient.PostAsJsonAsync("/api/cms/pages", new
        {
            Title           = "Test Page",
            Slug            = "test-page",
            Content         = "<p>Hello world</p>",
            MetaDescription = "A test page"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePage_WithoutTitle_Returns400()
    {
        await AdminClient.PostAsJsonAsync("/api/cms/pages", new
        {
            Title   = "",
            Content = "<p>No title</p>"
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PublishPage_SetsIsPublished()
    {
        // Create a page
        var createResp = await AdminClient.PostAsJsonAsync("/api/cms/pages", new
        {
            Title   = "Publishable Page",
            Content = "<p>Content</p>"
        });
        createResp.EnsureSuccessStatusCode();
        var page = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id   = page.GetProperty("id").GetInt32();

        // Publish it
        var publishResp = await AdminClient.PutAsync($"/api/cms/pages/{id}/publish", null);
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PublicSiteConfig_UnknownSlug_Returns404()
    {
        var response = await Client.GetAsync("/pub/site/nonexistent-slug-xyz");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ByHostEndpoint_UnknownHost_Returns404()
    {
        var response = await Client.GetAsync("/pub/site/by-host/www.unknownsite.com");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
