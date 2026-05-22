using System.IdentityModel.Tokens.Jwt;
using Lidstroem.Plugins.Communication.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Communication.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly DbContext _context;

    public NotificationsController(DbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Notification>>> GetMyNotifications(
        [FromQuery] bool unreadOnly = false)
    {
        var actorId = GetActorId();
        if (actorId == null) return Unauthorized();

        var query = _context.Set<Notification>().Where(n => n.ActorId == actorId.Value);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        return Ok(await query.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync());
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var actorId = GetActorId();
        if (actorId == null) return Unauthorized();

        return Ok(await _context.Set<Notification>()
            .CountAsync(n => n.ActorId == actorId.Value && !n.IsRead));
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var actorId = GetActorId();
        if (actorId == null) return Unauthorized();

        var notification = await _context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id && n.ActorId == actorId.Value);

        if (notification == null) return NotFound();

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private int? GetActorId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
