using System.IdentityModel.Tokens.Jwt;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Invitations.DTOs;
using Lidstroem.Plugins.Invitations.Entities;
using Lidstroem.Plugins.Invitations.Services;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Lidstroem.Plugins.Invitations.Controllers;

[Route("api/invitations")]
[ApiController]
public class InvitationsController : ControllerBase
{
    private readonly InvitationService _service;
    private readonly DbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _config;

    public InvitationsController(
        InvitationService service,
        DbContext context,
        ITenantContext tenantContext,
        IConfiguration config)
    {
        _service = service;
        _context = context;
        _tenantContext = tenantContext;
        _config = config;
    }

    [HttpPost]
    [Authorize]
    [RequirePermission("Invitations.Send")]
    public async Task<ActionResult<Invitation>> SendInvitation(SendInvitationDto dto)
    {
        var actorId = GetActorId();
        if (actorId == null) return Unauthorized();

        var baseUrl = _config["App:BaseUrl"] ?? "https://localhost:7209";

        try
        {
            var invitation = await _service.CreateAsync(
                dto.Email, _tenantContext.TenantId, actorId.Value, dto.RoleName, baseUrl);

            return CreatedAtAction(nameof(GetInvitations), new { id = invitation.Id }, invitation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    [Authorize]
    [RequirePermission("Invitations.View")]
    public async Task<ActionResult<IEnumerable<Invitation>>> GetInvitations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        return Ok(await _context.Set<Invitation>()
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync());
    }

    [HttpDelete("{id}")]
    [Authorize]
    [RequirePermission("Invitations.Send")]
    public async Task<IActionResult> RevokeInvitation(int id)
    {
        var invitation = await _context.Set<Invitation>().FindAsync(id);
        if (invitation == null) return NotFound();
        if (invitation.Status != InvitationStatus.Pending)
            return BadRequest("Only pending invitations can be revoked.");

        invitation.Status = InvitationStatus.Revoked;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<ActionResult> AcceptInvitation(AcceptInvitationDto dto)
    {
        try
        {
            var actor = await _service.AcceptAsync(dto.Token, dto.DisplayName, dto.Password);
            return Ok(new { Message = "Account created. You can now log in.", ActorId = actor.Id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("validate/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult> ValidateToken(string token)
    {
        var invitation = await _context.Set<Invitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation == null || !invitation.IsUsable)
            return BadRequest("Invitation is invalid or has expired.");

        return Ok(new { invitation.Email, invitation.ExpiresAt });
    }

    private int? GetActorId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
