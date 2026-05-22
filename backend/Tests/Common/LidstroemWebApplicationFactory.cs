using Lidstroem.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Lidstroem.Tests.Common;

/// <summary>
/// Boots the entire Lidstroem WebAPI — all plugins, all middleware —
/// but replaces SQL Server with a per-test SQLite in-memory database.
/// Each test class that needs the factory gets a fresh database.
/// </summary>
public class LidstroemWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"LidstroemTest_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the real AppDbContext registration
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContext>();

            // Replace with SQLite in-memory
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_dbName};Mode=Memory;Cache=Shared"));

            services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
        });

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            // Provide test configuration values
            var settings = new Dictionary<string, string?>
            {
                ["Auth:Jwt:Secret"]              = "test-secret-minimum-32-characters-long",
                ["Auth:Jwt:Issuer"]              = "lidstroem-test",
                ["Auth:Jwt:Audience"]            = "lidstroem-test",
                ["Auth:Jwt:AccessTokenMinutes"]  = "60",
                ["Auth:Jwt:RefreshTokenDays"]    = "7",
                ["SuperAdmin:Email"]             = "admin@test.local",
                ["SuperAdmin:Password"]          = "TestPassword123!",
                ["Communication:Smtp:Host"]      = "localhost",
                ["Communication:Smtp:Port"]      = "1025",
                ["Communication:Smtp:EnableSsl"] = "false",
                ["Resources:Storage:LocalPath"]  = Path.Combine(Path.GetTempPath(), $"lid-test-{_dbName}"),
            };

            config.AddInMemoryCollection(settings);
        });
    }

    /// <summary>
    /// Creates and migrates the test database, then seeds the SuperAdmin actor.
    /// Call once per test class via a shared fixture.
    /// </summary>
    public async Task InitialiseDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Returns an HttpClient with a valid Bearer token for the default SuperAdmin.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string? identifier = null, string? password = null)
    {
        var client = CreateClient();
        identifier ??= "admin@test.local";
        password   ??= "TestPassword123!";

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Identifier = identifier,
            Password   = password
        });

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Test login failed for {identifier}: {response.StatusCode}");

        var body  = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    private record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
}
