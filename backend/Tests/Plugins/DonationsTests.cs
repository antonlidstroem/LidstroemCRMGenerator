using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class DonationsTests : IntegrationTestBase
{
    public DonationsTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("Actor")]
    [InlineData("Company")]
    [InlineData("Random")]
    public async Task PostDonation_WithInvalidTargetType_Returns400(string badType)
    {
        await AdminClient.PostAsJsonAsync("/api/donations", new
        {
            Amount     = 100m,
            Currency   = "SEK",
            TargetId   = 1,
            TargetType = badType
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDonation_WithProjectTarget_Returns201()
    {
        var projectId = await new ProjectBuilder().WithTitle("Fund me").BuildAsync(AdminClient);
        var actorId   = await new ActorBuilder().WithEmail($"donor-{Guid.NewGuid():N}@test.local").BuildAsync(AdminClient);

        var response = await AdminClient.PostAsJsonAsync("/api/donations", new
        {
            Amount     = 500m,
            Currency   = "SEK",
            DonorId    = actorId,
            DonorType  = "Actor",
            TargetId   = projectId,
            TargetType = "Project"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostDonation_WithActivityTarget_Returns201()
    {
        var projectId  = await new ProjectBuilder().WithTitle("Activity Project").BuildAsync(AdminClient);
        var activityId = await new ActivityBuilder().ForProject(projectId).BuildAsync(AdminClient);
        var actorId    = await new ActorBuilder().WithEmail($"donor2-{Guid.NewGuid():N}@test.local").BuildAsync(AdminClient);

        var response = await AdminClient.PostAsJsonAsync("/api/donations", new
        {
            Amount     = 250m,
            Currency   = "EUR",
            DonorId    = actorId,
            DonorType  = "Actor",
            TargetId   = activityId,
            TargetType = "Activity"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostDonation_WithInvalidDonorType_Returns400()
    {
        var projectId = await new ProjectBuilder().WithTitle("Test project").BuildAsync(AdminClient);

        await AdminClient.PostAsJsonAsync("/api/donations", new
        {
            Amount     = 100m,
            Currency   = "SEK",
            DonorId    = 1,
            DonorType  = "Company",   // not allowed
            TargetId   = projectId,
            TargetType = "Project"
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDonation_TargetIdWithoutTargetType_Returns400()
    {
        await AdminClient.PostAsJsonAsync("/api/donations", new
        {
            Amount   = 100m,
            Currency = "SEK",
            TargetId = 1
            // TargetType missing
        })
        .ShouldReturn(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Donation_AppearsInActorExtensions()
    {
        var actorId   = await new ActorBuilder().WithEmail($"extactor-{Guid.NewGuid():N}@test.local").BuildAsync(AdminClient);
        var projectId = await new ProjectBuilder().WithTitle("Ext project").BuildAsync(AdminClient);

        await new DonationBuilder()
            .FromActor(actorId)
            .ToProject(projectId)
            .WithAmount(999m)
            .BuildAsync(AdminClient);

        var actor = await AdminClient.GetJsonAsync($"/api/actors/{actorId}");

        actor.TryGetProperty("extensions", out var extensions).Should().BeTrue();
        var hasDonations = extensions.EnumerateObject()
            .Any(p => p.Name.Contains("Donation", StringComparison.OrdinalIgnoreCase));
        hasDonations.Should().BeTrue("Actor detail should include Donations extension");
    }

    [Fact]
    public async Task Donation_AppearsInProjectExtensions()
    {
        var projectId = await new ProjectBuilder().WithTitle("Donor project").BuildAsync(AdminClient);
        var actorId   = await new ActorBuilder().WithEmail($"projdonor-{Guid.NewGuid():N}@test.local").BuildAsync(AdminClient);

        await new DonationBuilder()
            .FromActor(actorId)
            .ToProject(projectId)
            .WithAmount(150m)
            .BuildAsync(AdminClient);

        var project = await AdminClient.GetJsonAsync($"/api/projects/{projectId}");

        project.TryGetProperty("extensions", out var extensions).Should().BeTrue();
        var hasDonations = extensions.EnumerateObject()
            .Any(p => p.Name.Contains("Donation", StringComparison.OrdinalIgnoreCase));
        hasDonations.Should().BeTrue("Project detail should include Donations extension");
    }

    [Fact]
    public async Task Donation_AppearsInActivityExtensions()
    {
        var projectId  = await new ProjectBuilder().WithTitle("Activity Donations Project").BuildAsync(AdminClient);
        var activityId = await new ActivityBuilder().ForProject(projectId).BuildAsync(AdminClient);
        var actorId    = await new ActorBuilder().WithEmail($"actdonor-{Guid.NewGuid():N}@test.local").BuildAsync(AdminClient);

        await new DonationBuilder()
            .FromActor(actorId)
            .ToActivity(activityId)
            .WithAmount(75m)
            .BuildAsync(AdminClient);

        var activity = await AdminClient.GetJsonAsync($"/api/activities/{activityId}");

        activity.TryGetProperty("extensions", out var extensions).Should().BeTrue();
        var hasDonations = extensions.EnumerateObject()
            .Any(p => p.Name.Contains("Donation", StringComparison.OrdinalIgnoreCase));
        hasDonations.Should().BeTrue("Activity detail should include Donations extension");
    }
}
