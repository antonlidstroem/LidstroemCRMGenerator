using Lidstroem.Core.Auth;
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
    private readonly DbContext         _context;
    private readonly IPermissionService _permissionService;
    private readonly IAuthContext       _auth;

    public RbacController(
        DbContext context,
        IPermissionService permissionService,
        IAuthContext auth)
    {
        _context           = context;
        _permissionService = permissionService;
        _auth              = auth;
    }

    [HttpGet("permissions")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<IEnumerable<Permission>>> GetPermissions() =>
        Ok(await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .ToListAsync());

    /// <summary>
    /// BUG-35 FIX: Previously returned ALL roles including SuperAdmin system roles.
    /// A tenant admin could assign SuperAdmin.ManageTenants to a regular CRM user.
    ///
    /// Now:
    ///   - SuperAdmin sees all roles (needs full list for system management)
    ///   - Regular actors see only non-system roles (IsSystemRole = false)
    ///   - Roles are also filtered to the requesting tenant via TenantId
    /// </summary>
    [HttpGet("roles")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<IEnumerable<Role>>> GetRoles(
        [FromQuery] Guid? tenantId = null)
    {
        var query = _context.Set<Role>()
            .IgnoreQueryFilters()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .AsQueryable();

        // BUG-35 FIX: Non-SuperAdmin callers only see non-system roles
        if (!_auth.HasPermission("SuperAdmin.ManageTenants"))
            query = query.Where(r => !r.IsSystemRole);

        // Filter by tenant when managing a specific tenant's actors
        if (tenantId.HasValue)
            query = query.Where(r =>
                r.TenantId == tenantId.Value ||
                r.TenantId == Lidstroem.Core.Constants.TenantConstants.SystemTenantId);

        return Ok(await query.OrderBy(r => r.DisplayName).ToListAsync());
    }

    [HttpPost("roles")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<Role>> CreateRole(CreateRoleDto dto)
    {
        var role = new Role
        {
            Name        = dto.Name,
            DisplayName = dto.DisplayName,
            IsSystemRole = false
        };

        if (dto.PermissionNames?.Count > 0)
        {
            var permissions = await _context.Set<Permission>()
                .IgnoreQueryFilters()
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

    // POINT 3 FIX: Added DELETE so TenantDetail can remove roles.
    // Returns 409 Conflict if the role still has active assignments — safer than
    // cascade deleting assignments without warning the caller.
    [HttpDelete("roles/{id:int}")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var role = await _context.Set<Role>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound();

        if (role.IsSystemRole) return Forbid();

        var hasAssignments = await _context.Set<ActorRoleAssignment>()
            .IgnoreQueryFilters()
            .AnyAsync(a => a.RoleId == id);

        if (hasAssignments)
            return Conflict("Role still has active assignments. Remove them first.");

        _context.Set<Role>().Remove(role);
        await _context.SaveChangesAsync();
        return NoContent();
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

    [HttpGet("actor/{actorId}/roles")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<IEnumerable<string>>> GetActorRoles(
        int actorId, [FromQuery] Guid tenantId)
    {
        var roleNames = await _context.Set<ActorRoleAssignment>()
            .IgnoreQueryFilters()
            .Where(a => a.ActorId == actorId && a.TenantId == tenantId)
            .Include(a => a.Role)
            .Select(a => a.Role.Name)
            .ToListAsync();

        return Ok(roleNames);
    }

    [HttpGet("actors/credentials-status")]
    [RequirePermission("SuperAdmin.ManageTenants")]
    public async Task<ActionResult<IEnumerable<int>>> GetCredentialsStatus(
        [FromQuery] string actorIds)
    {
        if (string.IsNullOrWhiteSpace(actorIds)) return Ok(Array.Empty<int>());

        var ids = actorIds.Split(',')
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var withCredentials = await _context.Set<ActorCredentials>()
            .IgnoreQueryFilters()
            .Where(c => ids.Contains(c.ActorId) && c.IsActive && c.PasswordHash != null)
            .Select(c => c.ActorId)
            .Distinct()
            .ToListAsync();

        return Ok(withCredentials);
    }

    [HttpGet("my-permissions")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyCollection<string>>> GetMyPermissions(
        [FromQuery] Guid tenantId)
    {
        var sub = User.FindFirst(
            System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(sub, out var actorId)) return Unauthorized();
        return Ok(await _permissionService.GetPermissionsAsync(actorId, tenantId));
    }
}
