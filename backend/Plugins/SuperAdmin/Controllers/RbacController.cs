using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.RBAC.Entities;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.SuperAdmin.Controllers;

public class CreateRoleDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string>? PermissionNames { get; set; }
}

public class AssignRoleDto
{
    public int ActorId { get; set; }
    public Guid TenantId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

[Route("api/rbac")]
[ApiController]
[Authorize]
public class RbacController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IPermissionService _permissionService;

    public RbacController(DbContext context, IPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    [HttpGet("permissions")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<IEnumerable<Permission>>> GetPermissions() =>
        Ok(await _context.Set<Permission>().IgnoreQueryFilters()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .ToListAsync());

    [HttpGet("roles")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<IEnumerable<Role>>> GetRoles() =>
        Ok(await _context.Set<Role>()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .ToListAsync());

    [HttpPost("roles")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<Role>> CreateRole(CreateRoleDto dto)
    {
        var role = new Role { Name = dto.Name, DisplayName = dto.DisplayName };

        if (dto.PermissionNames?.Count > 0)
        {
            var permissions = await _context.Set<Permission>().IgnoreQueryFilters()
                .Where(p => dto.PermissionNames.Contains(p.Name) && p.IsActive)
                .ToListAsync();

            role.RolePermissions = permissions
                .Select(p => new RolePermission { Permission = p })
                .ToList();
        }

        _context.Set<Role>().Add(role);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRoles), new { id = role.Id }, role);
    }

    [HttpPost("assign")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<IActionResult> AssignRole(AssignRoleDto dto)
    {
        await _permissionService.AssignRoleAsync(dto.ActorId, dto.TenantId, dto.RoleName);
        return NoContent();
    }

    [HttpPost("revoke")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<IActionResult> RevokeRole(AssignRoleDto dto)
    {
        await _permissionService.RevokeRoleAsync(dto.ActorId, dto.TenantId, dto.RoleName);
        return NoContent();
    }

    [HttpGet("actor/{actorId}/permissions")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<IReadOnlyCollection<string>>> GetActorPermissions(
        int actorId, [FromQuery] Guid tenantId) =>
        Ok(await _permissionService.GetPermissionsAsync(actorId, tenantId));

    /// <summary>
    /// Returns the calling actor's own permissions. No elevated permission required —
    /// every authenticated user may query their own permissions.
    /// Used by the frontend PermissionService at boot.
    /// Previously the frontend called /actor/{id}/permissions which requires
    /// SuperAdmin.ManageTenants — non-admin users got 403, leaving _permissions
    /// null and hiding all permission-gated UI for every normal user.
    /// </summary>
    [HttpGet("my-permissions")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyCollection<string>>> GetMyPermissions(
        [FromQuery] Guid tenantId)
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(sub, out var actorId)) return Unauthorized();
        return Ok(await _permissionService.GetPermissionsAsync(actorId, tenantId));
    }
}
