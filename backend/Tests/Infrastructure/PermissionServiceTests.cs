using FluentAssertions;
using Lidstroem.Core.Constants;
using Lidstroem.Core.Entities;
using Lidstroem.Infrastructure.RBAC.Entities;
using Lidstroem.Infrastructure.RBAC.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Lidstroem.Tests.Infrastructure;

public class PermissionServiceTests : InfrastructureTestBase
{
    private readonly PermissionService _service;
    private readonly Guid _tenantId;

    public PermissionServiceTests()
    {
        _tenantId = TenantA;
        _service  = new PermissionService(Db, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<int> SeedActorAsync(string email = "test@test.com")
    {
        var actor = new Actor { DisplayName = "Test", Email = email, TenantId = _tenantId };
        Db.Set<Actor>().Add(actor);
        await Db.SaveChangesAsync();
        return actor.Id;
    }

    private async Task<int> SeedRoleWithPermissionAsync(string roleName, string permissionName)
    {
        var permission = new Permission
        {
            Name        = permissionName,
            DisplayName = permissionName,
            Description = permissionName,
            Category    = "Test",
            IsActive    = true,
            TenantId    = TenantConstants.SystemTenantId
        };
        Db.Set<Permission>().Add(permission);
        await Db.SaveChangesAsync();

        var role = new Role
        {
            Name        = roleName,
            DisplayName = roleName,
            TenantId    = _tenantId,
            RolePermissions = new List<RolePermission>
            {
                new() { Permission = permission }
            }
        };
        Db.Set<Role>().Add(role);
        await Db.SaveChangesAsync();

        return role.Id;
    }

    private async Task AssignRoleAsync(int actorId, int roleId)
    {
        Db.Set<ActorRoleAssignment>().Add(new ActorRoleAssignment
        {
            ActorId  = actorId,
            RoleId   = roleId,
            TenantId = _tenantId
        });
        await Db.SaveChangesAsync();
    }

    [Fact]
    public async Task HasPermission_WhenActorHasRole_ReturnsTrue()
    {
        var actorId = await SeedActorAsync();
        var roleId  = await SeedRoleWithPermissionAsync("Editor", "Projects.Edit");
        await AssignRoleAsync(actorId, roleId);

        var result = await _service.HasPermissionAsync(actorId, _tenantId, "Projects.Edit");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermission_WhenActorHasNoRole_ReturnsFalse()
    {
        var actorId = await SeedActorAsync("norole@test.com");

        var result = await _service.HasPermissionAsync(actorId, _tenantId, "Projects.Edit");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermission_WhenPermissionInactive_ReturnsFalse()
    {
        var actorId = await SeedActorAsync("inactive@test.com");

        // Seed an inactive permission
        var permission = new Permission
        {
            Name = "Inactive.Permission", DisplayName = "x", Description = "x",
            Category = "Test", IsActive = false, TenantId = TenantConstants.SystemTenantId
        };
        Db.Set<Permission>().Add(permission);
        var role = new Role
        {
            Name = "InactiveRole", DisplayName = "x", TenantId = _tenantId,
            RolePermissions = new List<RolePermission> { new() { Permission = permission } }
        };
        Db.Set<Role>().Add(role);
        await Db.SaveChangesAsync();
        await AssignRoleAsync(actorId, role.Id);

        var result = await _service.HasPermissionAsync(actorId, _tenantId, "Inactive.Permission");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermission_WhenRoleInDifferentTenant_ReturnsFalse()
    {
        var actorId = await SeedActorAsync("crosstenancy@test.com");
        var roleId  = await SeedRoleWithPermissionAsync("AdminRole", "SuperAdmin.DoEverything");

        // Assign role for TenantB, not TenantA
        Db.Set<ActorRoleAssignment>().Add(new ActorRoleAssignment
        {
            ActorId  = actorId,
            RoleId   = roleId,
            TenantId = TenantB   // different tenant!
        });
        await Db.SaveChangesAsync();

        var result = await _service.HasPermissionAsync(actorId, TenantA, "SuperAdmin.DoEverything");

        result.Should().BeFalse("Role assigned in TenantB should not grant permission in TenantA");
    }

    [Fact]
    public async Task GetPermissions_ReturnsAllPermissionsForActor()
    {
        var actorId = await SeedActorAsync("multi@test.com");
        var roleId  = await SeedRoleWithPermissionAsync("Viewer", "Projects.View");
        await AssignRoleAsync(actorId, roleId);

        var permissions = await _service.GetPermissionsAsync(actorId, _tenantId);

        permissions.Should().Contain("Projects.View");
    }

    [Fact]
    public async Task InvalidateCache_ForcesRecheckOnNextCall()
    {
        var actorId = await SeedActorAsync("cache@test.com");

        // First call — no permission
        var before = await _service.HasPermissionAsync(actorId, _tenantId, "Projects.View");
        before.Should().BeFalse();

        // Add role
        var roleId = await SeedRoleWithPermissionAsync("LateRole", "Projects.View");
        await AssignRoleAsync(actorId, roleId);

        // Without invalidation, cached result would still return false
        _service.InvalidateCache(actorId, _tenantId);

        var after = await _service.HasPermissionAsync(actorId, _tenantId, "Projects.View");
        after.Should().BeTrue("Permission should be found after cache invalidation");
    }

    [Fact]
    public async Task AssignRole_WhenAlreadyAssigned_DoesNotDuplicate()
    {
        var actorId = await SeedActorAsync("dup@test.com");
        var roleId  = await SeedRoleWithPermissionAsync("DupRole", "Test.Permission");
        await AssignRoleAsync(actorId, roleId);

        // Assign same role again — should not throw
        var act = () => _service.AssignRoleAsync(actorId, _tenantId, "DupRole");

        await act.Should().NotThrowAsync();

        // Should still have exactly one assignment
        var assignments = Db.Set<ActorRoleAssignment>()
            .Where(a => a.ActorId == actorId && a.RoleId == roleId)
            .ToList();
        assignments.Should().HaveCount(1);
    }
}
