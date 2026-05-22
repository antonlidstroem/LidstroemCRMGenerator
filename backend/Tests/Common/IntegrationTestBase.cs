using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Lidstroem.Infrastructure.Data;
using Xunit;

namespace Lidstroem.Tests.Common;

/// <summary>
/// Base class for integration tests.
/// Each test class gets a single factory (one DB) shared across all tests in that class.
/// Tests within a class run sequentially to avoid DB race conditions.
/// </summary>
[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly LidstroemWebApplicationFactory Factory;
    protected HttpClient Client = null!;
    protected HttpClient AdminClient = null!;

    protected IntegrationTestBase(LidstroemWebApplicationFactory factory)
    {
        Factory = factory;
    }

    public async Task InitializeAsync()
    {
        await Factory.InitialiseDatabaseAsync();
        AdminClient = await Factory.CreateAuthenticatedClientAsync();
        Client      = Factory.CreateClient(); // unauthenticated
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected async Task<T> PostAndDeserialise<T>(
        HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    protected async Task<T> GetAndDeserialise<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    protected AppDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

/// <summary>
/// xUnit collection — ensures all integration tests share one factory instance
/// but can still parallelise between collections.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<LidstroemWebApplicationFactory> { }
