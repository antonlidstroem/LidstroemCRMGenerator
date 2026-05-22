using System.IdentityModel.Tokens.Jwt;
using Lidstroem.Core.Entities;
using Lidstroem.Core.GDPR;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.GDPR.Entities;
using Lidstroem.Shared.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.GDPR.Controllers;

[Route("api/gdpr")]
[ApiController]
[Authorize]
public class GdprController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly DbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GdprController(
        IMediator mediator,
        DbContext context,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _mediator = mediator;
        _context = context;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    // FIX #5: Dedicated self-forget route — no permission needed, actor forgets themselves.
    [HttpPost("forget/me")]
    public async Task<ActionResult<GdprResult>> ForgetSelf()
    {
        var requestingActorId = GetActorId();
        if (requestingActorId == null) return Unauthorized();

        return await RunForgetAsync(requestingActorId.Value, "Actor");
    }

    // FIX #5: Admin forget — explicit [RequirePermission] attribute replaces fragile service-locator.
    [HttpPost("forget/{subjectId}")]
    [RequirePermission("GDPR.Forget")]
    public async Task<ActionResult<GdprResult>> ForgetSubject(
        int subjectId, [FromQuery] string subjectType = "Actor")
    {
        return await RunForgetAsync(subjectId, subjectType);
    }

    private async Task<ActionResult<GdprResult>> RunForgetAsync(int subjectId, string subjectType)
    {
        string? email = null;
        if (string.Equals(subjectType, "Actor", StringComparison.OrdinalIgnoreCase))
        {
            var actor = await _context.Set<Actor>()
                .FirstOrDefaultAsync(a => a.Id == subjectId
                                       && a.TenantId == _tenantContext.TenantId);
            if (actor == null) return NotFound();
            email = actor.Email;
        }

        var command = new ForgetSubjectCommand
        {
            SubjectId = subjectId,
            SubjectType = subjectType,
            TenantId = _tenantContext.TenantId,
            Email = email,
            RequestedByActorId = GetActorId()
        };

        var result = await _mediator.Send(command);

        if (!result.AllSucceeded)
            return StatusCode(207, new
            {
                Message = "Forget operation partially failed.",
                result.Results,
                result.Failed
            });

        return Ok(result);
    }

    [HttpGet("log")]
    [RequirePermission("GDPR.ViewLog")]
    public async Task<ActionResult<IEnumerable<GdprLog>>> GetLog(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _context.Set<GdprLog>().IgnoreQueryFilters()
            .Where(g => g.TenantId == _tenantContext.TenantId)
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync());

    private int? GetActorId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
