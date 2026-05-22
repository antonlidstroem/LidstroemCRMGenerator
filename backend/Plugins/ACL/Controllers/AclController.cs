using System.IdentityModel.Tokens.Jwt;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.ACL.DTOs;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lidstroem.Plugins.ACL.Controllers;

[Route("api/acl")]
[ApiController]
[Authorize]
public class AclController : ControllerBase
{
    private readonly IAclService _acl;
    private readonly IPermissionService _permissions;
    private readonly ITenantContext _tenantContext;

    public AclController(IAclService acl, IPermissionService permissions, ITenantContext tenantContext)
    {
        _acl = acl;
        _permissions = permissions;
        _tenantContext = tenantContext;
    }

    // FIX #4: Caller must either own the resource (be the original granter) or hold ACL.ManageGrants.
    [HttpPost("grant")]
    [RequirePermission("ACL.Grant")]
    public async Task<IActionResult> Grant(GrantAclDto dto)
    {
        var grantedBy = GetActorId();
        if (grantedBy == null) return Unauthorized();

        // Verify the caller has admin rights OR is the existing resource owner
        var isAdmin = await _permissions.HasPermissionAsync(
            grantedBy.Value, _tenantContext.TenantId, "ACL.ManageGrants");

        var isOwner = await _acl.IsGranterAsync(grantedBy.Value, dto.ResourceId, dto.ResourceType);

        if (!isAdmin && !isOwner)
            return Forbid();

        await _acl.GrantAsync(new AclEntry(
            dto.ResourceId, dto.ResourceType,
            grantedBy.Value, dto.GrantedToActorId,
            dto.Action, dto.ExpiresAt));

        return NoContent();
    }

    // FIX #4: Revoke similarly requires ACL.ManageGrants or the original granter
    [HttpDelete("revoke")]
    [RequirePermission("ACL.Grant")]
    public async Task<IActionResult> Revoke(RevokeAclDto dto)
    {
        var actorId = GetActorId();
        if (actorId == null) return Unauthorized();

        var isAdmin = await _permissions.HasPermissionAsync(
            actorId.Value, _tenantContext.TenantId, "ACL.ManageGrants");

        var isOwner = await _acl.IsGranterAsync(actorId.Value, dto.ResourceId, dto.ResourceType);

        if (!isAdmin && !isOwner)
            return Forbid();

        await _acl.RevokeAsync(dto.ResourceId, dto.ResourceType, dto.GrantedToActorId, dto.Action);
        return NoContent();
    }

    [HttpGet("resource/{resourceType}/{resourceId}")]
    [RequirePermission("ACL.View")]
    public async Task<ActionResult<IReadOnlyList<AclEntry>>> GetGrants(string resourceType, int resourceId) =>
        Ok(await _acl.GetGrantsAsync(resourceId, resourceType));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<AclEntry>>> GetMyGrants()
    {
        var actorId = GetActorId();
        if (actorId == null) return Unauthorized();
        return Ok(await _acl.GetActorGrantsAsync(actorId.Value));
    }

    private int? GetActorId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
