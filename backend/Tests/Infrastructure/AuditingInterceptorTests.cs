using FluentAssertions;
using Lidstroem.Core.Constants;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.Data;
using Lidstroem.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Lidstroem.Tests.Infrastructure;

public class AuditingInterceptorTests
{
    private static AppDbContext CreateContext(string userId)
        => CreateContextWithDb(userId, $"AuditTest_{Guid.NewGuid():N}");

    // BUG FIX #21: Added overload that accepts a caller-controlled DB name so that two
    // contexts in the same test can share state (insert in one, read-back in another).
    private static AppDbContext CreateContextWithDb(string userId, string dbName)
    {
        var userCtx = new Mock<ICurrentUserContext>();
        userCtx.Setup(u => u.UserId).Returns(userId);

        var tenantCtx = new Mock<ITenantContext>();
        tenantCtx.Setup(t => t.TenantId).Returns(TenantConstants.SystemTenantId);
        tenantCtx.Setup(t => t.IsSystemContext).Returns(true);
        tenantCtx.Setup(t => t.OwnerId).Returns((Guid?)null);

        var interceptor = new AuditingInterceptor(userCtx.Object);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbName};Mode=Memory;Cache=Shared")
            .AddInterceptors(interceptor)
            .Options;

        var ctx = new AppDbContext(options, tenantCtx.Object);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task OnCreate_SetsCreatedAtAndCreatedBy()
    {
        using var ctx = CreateContext("user-123");
        var before = DateTime.UtcNow.AddSeconds(-1);

        ctx.Set<Actor>().Add(new Actor
        {
            DisplayName = "Test",
            Email       = "audit@test.com",
            TenantId    = TenantConstants.SystemTenantId
        });
        await ctx.SaveChangesAsync();

        var actor = await ctx.Set<Actor>()
            .IgnoreQueryFilters()
            .FirstAsync(a => a.Email == "audit@test.com");

        actor.CreatedAt.Should().BeAfter(before);
        actor.CreatedBy.Should().Be("user-123");
        actor.UpdatedAt.Should().NotBeNull();
        actor.ModifiedBy.Should().Be("user-123");
    }

    [Fact]
    public async Task OnUpdate_SetsUpdatedAtAndModifiedBy()
    {
        // BUG FIX #21 (continued): The two contexts must share the same in-memory SQLite DB
        // so the actor inserted by the first context is visible to the second. We use a
        // test-scoped unique name so it doesn't pollute any other test.
        var sharedDb = $"AuditTest_Update_{Guid.NewGuid():N}";

        int actorId;
        using (var createCtx = CreateContextWithDb("creator-111", sharedDb))
        {
            var actor = new Actor
            {
                DisplayName = "Original",
                Email       = "update@test.com",
                TenantId    = TenantConstants.SystemTenantId
            };
            createCtx.Set<Actor>().Add(actor);
            await createCtx.SaveChangesAsync();
            actorId = actor.Id;

            actor.CreatedBy.Should().Be("creator-111");
        }

        using (var updateCtx = CreateContextWithDb("updater-456", sharedDb))
        {
            var loaded = await updateCtx.Set<Actor>().FindAsync(actorId)
                ?? throw new InvalidOperationException("Actor not found after insert");

            var originalUpdatedAt = loaded.UpdatedAt;
            await Task.Delay(10);

            loaded.DisplayName = "Updated";
            await updateCtx.SaveChangesAsync();

            loaded.UpdatedAt.Should().BeAfter(originalUpdatedAt ?? DateTime.MinValue);
            loaded.ModifiedBy.Should().Be("updater-456");
            loaded.CreatedBy.Should().Be("creator-111",
                "CreatedBy is stamped on the first save and must not be overwritten by later saves.");
        }
    }

    [Fact]
    public async Task OnCreate_WhenCreatedAtAlreadySet_DoesNotOverwrite()
    {
        using var ctx = CreateContext("user-789");
        var explicitTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var actor = new Actor
        {
            DisplayName = "Fixed",
            Email       = "fixed@test.com",
            TenantId    = TenantConstants.SystemTenantId,
            CreatedAt   = explicitTime
        };
        ctx.Set<Actor>().Add(actor);
        await ctx.SaveChangesAsync();

        actor.CreatedAt.Should().Be(explicitTime,
            "Explicitly set CreatedAt should not be overwritten by interceptor");
    }
}
