using FluentAssertions;
using Lidstroem.Core.Constants;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Lidstroem.Tests.Infrastructure;

/// <summary>
/// Verifies that the global query filter on AppDbContext correctly
/// isolates data between tenants. This is the most security-critical
/// test in the entire test suite.
/// </summary>
public class TenantIsolationTests
{
    private readonly Guid _tenantA = new("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private readonly Guid _tenantB = new("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    // BUG FIX #13: The original code used the hardcoded name "TenantIsolationTest" for
    // every SQLite in-memory database. Because SQLite in-memory databases with the same
    // name and Cache=Shared are the same database, data inserted by one test method leaked
    // into others and the tests became order-dependent. Use a unique name per instance so
    // each test gets a clean, isolated database — matching the pattern in AuditingInterceptorTests.
    private readonly string _dbName = $"TenantIsolationTest_{Guid.NewGuid():N}";

    private AppDbContext CreateContext(Guid tenantId, bool isSystem = false)
    {
        var mock = new Mock<ITenantContext>();
        mock.Setup(t => t.TenantId).Returns(tenantId);
        mock.Setup(t => t.IsSystemContext).Returns(isSystem);
        mock.Setup(t => t.OwnerId).Returns((Guid?)null);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName};Mode=Memory;Cache=Shared")
            .Options;

        return new AppDbContext(options, mock.Object);
    }

    [Fact]
    public async Task Actor_CreatedByTenantA_IsNotVisibleToTenantB()
    {
        // Arrange — seed actor in tenant A
        using var seedCtx = CreateContext(_tenantA);
        await seedCtx.Database.EnsureCreatedAsync();

        seedCtx.Set<Actor>().Add(new Actor
        {
            DisplayName = "Tenant A Actor",
            Email       = "a@tenant-a.com",
            TenantId    = _tenantA
        });
        await seedCtx.SaveChangesAsync();

        // Act — read from tenant B context
        using var tenantBCtx = CreateContext(_tenantB);
        var actors = await tenantBCtx.Set<Actor>().ToListAsync();

        // Assert
        actors.Should().NotContain(a => a.Email == "a@tenant-a.com",
            "Tenant B should not see Tenant A's actors");
    }

    [Fact]
    public async Task Actor_CreatedByTenantA_IsVisibleToTenantA()
    {
        using var ctx = CreateContext(_tenantA);
        await ctx.Database.EnsureCreatedAsync();

        ctx.Set<Actor>().Add(new Actor
        {
            DisplayName = "Own Actor",
            Email       = "own@tenant-a.com",
            TenantId    = _tenantA
        });
        await ctx.SaveChangesAsync();

        var actors = await ctx.Set<Actor>()
            .Where(a => a.Email == "own@tenant-a.com")
            .ToListAsync();

        actors.Should().ContainSingle();
    }

    [Fact]
    public async Task SystemContext_CanSeeAllTenants()
    {
        // Seed data for two tenants
        using var ctxA = CreateContext(_tenantA);
        await ctxA.Database.EnsureCreatedAsync();
        ctxA.Set<Actor>().Add(new Actor { DisplayName = "A", Email = "a@a.com", TenantId = _tenantA });
        await ctxA.SaveChangesAsync();

        using var ctxB = CreateContext(_tenantB);
        ctxB.Set<Actor>().Add(new Actor { DisplayName = "B", Email = "b@b.com", TenantId = _tenantB });
        await ctxB.SaveChangesAsync();

        // System context should bypass filter
        using var systemCtx = CreateContext(_tenantA, isSystem: true);
        var allActors = await systemCtx.Set<Actor>().IgnoreQueryFilters().ToListAsync();

        allActors.Should().Contain(a => a.Email == "a@a.com");
        allActors.Should().Contain(a => a.Email == "b@b.com");
    }

    [Fact]
    public async Task TenantId_IsAutoStamped_OnNewEntities()
    {
        using var ctx = CreateContext(_tenantA);
        await ctx.Database.EnsureCreatedAsync();

        // Add actor WITHOUT explicitly setting TenantId
        var actor = new Actor { DisplayName = "Auto", Email = "auto@test.com" };
        ctx.Set<Actor>().Add(actor);
        await ctx.SaveChangesAsync();

        actor.TenantId.Should().Be(_tenantA,
            "AppDbContext should auto-stamp TenantId on save");
    }
}
