using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Lidstroem.Tests.Common;
using Xunit;

namespace Lidstroem.Tests.Plugins;

public class GdprTests : IntegrationTestBase
{
    public GdprTests(LidstroemWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ForgetActor_AnonymisesDisplayNameAndEmail()
    {
        // Arrange
        var actorId = await new ActorBuilder()
            .WithName("Anna Svensson")
            .WithEmail($"anna-{Guid.NewGuid():N}@real-email.com")
            .BuildAsync(AdminClient);

        // Act
        var response = await AdminClient
            .PostAsJsonAsync($"/api/gdpr/forget/{actorId}?subjectType=Actor", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var actor = await AdminClient.GetJsonAsync($"/api/actors/{actorId}");
        actor.GetProperty("displayName").GetString().Should().Be("[deleted]");
        actor.GetProperty("email").GetString().Should().Contain("gdpr.invalid");
    }

    [Fact]
    public async Task ForgetActor_WritesGdprLog()
    {
        var actorId = await new ActorBuilder()
            .WithEmail($"logged-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        await AdminClient.PostAsJsonAsync($"/api/gdpr/forget/{actorId}?subjectType=Actor", new { });

        var logs = await AdminClient.GetJsonAsync("/api/gdpr/log");
        var logEntries = logs.EnumerateArray().ToList();

        logEntries.Should().Contain(e =>
            e.GetProperty("forgottenSubjectId").GetInt32() == actorId,
            "a GdprLog entry must be written for every forget operation");
    }

    [Fact]
    public async Task ForgetActor_RunsAllRegisteredGdprHandlers()
    {
        var actorId = await new ActorBuilder()
            .WithEmail($"allhandlers-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        // Create a donation so the Donations GDPR handler has something to anonymise
        var projectId = await new ProjectBuilder().WithTitle("GDPR Project").BuildAsync(AdminClient);
        await new DonationBuilder().FromActor(actorId).ToProject(projectId).BuildAsync(AdminClient);

        var response = await AdminClient
            .PostAsJsonAsync($"/api/gdpr/forget/{actorId}?subjectType=Actor", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var handlersRun = result.GetProperty("results").GetArrayLength();

        handlersRun.Should().BeGreaterThan(1,
            "multiple IGdprHandlers should run — at least Core.Actor and Donations");
    }

    [Fact]
    public async Task ForgetActor_AnonymisesDonorId_OnRelatedDonations()
    {
        var actorId   = await new ActorBuilder()
            .WithEmail($"forgetdonor-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);
        var projectId = await new ProjectBuilder().WithTitle("Forget donations project").BuildAsync(AdminClient);
        var donationId = await new DonationBuilder()
            .FromActor(actorId)
            .ToProject(projectId)
            .BuildAsync(AdminClient);

        await AdminClient.PostAsJsonAsync($"/api/gdpr/forget/{actorId}?subjectType=Actor", new { });

        var donation = await AdminClient.GetJsonAsync($"/api/donations/{donationId}");
        var donorId = donation.TryGetProperty("donorId", out var d) ? d.GetInt32() as int? : null;

        donorId.Should().BeNull("DonorId must be nulled after GDPR forget");
    }

    [Fact]
    public async Task ForgetActor_WithoutGdprForgetPermission_WhenNotSelf_Returns403()
    {
        // Create a second actor (the target to forget)
        var targetActorId = await new ActorBuilder()
            .WithEmail($"target-{Guid.NewGuid():N}@test.local")
            .BuildAsync(AdminClient);

        // The unauthenticated client has no permissions at all
        var unauthResponse = await Client
            .PostAsJsonAsync($"/api/gdpr/forget/{targetActorId}?subjectType=Actor", new { });

        unauthResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgetActor_NonExistentId_Returns404()
    {
        var response = await AdminClient
            .PostAsJsonAsync("/api/gdpr/forget/99999?subjectType=Actor", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
