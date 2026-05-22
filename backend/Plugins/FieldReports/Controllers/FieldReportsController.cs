using Lidstroem.Plugins.FieldReports.DTOs;
using Lidstroem.Plugins.FieldReports.Entities;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.FieldReports.Controllers;

[Route("api/fieldreports")]
[ApiController]
[Authorize]
public class FieldReportsController : ControllerBase
{
    private readonly DbContext _context;

    public FieldReportsController(DbContext context) => _context = context;

    [HttpGet]
    [RequirePermission("FieldReports.View")]
    public async Task<ActionResult<IEnumerable<FieldReport>>> GetFieldReports(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        return Ok(await _context.Set<FieldReport>()
            .Include(r => r.Contributors)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync());
    }

    [HttpGet("{id}")]
    [RequirePermission("FieldReports.View")]
    public async Task<ActionResult<FieldReport>> GetFieldReport(int id)
    {
        var report = await _context.Set<FieldReport>()
            .Include(r => r.Contributors)
            .FirstOrDefaultAsync(r => r.Id == id);

        return report == null ? NotFound() : Ok(report);
    }

    [HttpPost]
    [RequirePermission("FieldReports.Create")]
    public async Task<ActionResult<FieldReport>> PostFieldReport(FieldReportDto dto)
    {
        var report = new FieldReport
        {
            Title = dto.Title,
            Content = dto.Content,
            AuthorActorId = dto.AuthorActorId,
            ActivityId = dto.ActivityId,
            ActivityType = dto.ActivityType,
            ContextId = dto.ContextId,
            ContextType = dto.ContextType
        };

        if (dto.ContributorActorIds?.Count > 0)
        {
            report.Contributors = dto.ContributorActorIds
                .Select(actorId => new FieldReportContributor { ActorId = actorId })
                .ToList();
        }

        _context.Set<FieldReport>().Add(report);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetFieldReport), new { id = report.Id }, report);
    }

    [HttpPut("{id}")]
    [RequirePermission("FieldReports.Edit")]
    public async Task<IActionResult> PutFieldReport(int id, FieldReportDto dto)
    {
        if (id != dto.Id) return BadRequest("Route id does not match body id.");

        var report = await _context.Set<FieldReport>()
            .Include(r => r.Contributors)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return NotFound();

        report.Title = dto.Title;
        report.Content = dto.Content;
        report.AuthorActorId = dto.AuthorActorId;
        report.ActivityId = dto.ActivityId;
        report.ActivityType = dto.ActivityType;
        report.ContextId = dto.ContextId;
        report.ContextType = dto.ContextType;

        if (dto.ContributorActorIds != null)
        {
            report.Contributors.Clear();
            report.Contributors = dto.ContributorActorIds
                .Select(actorId => new FieldReportContributor
                {
                    FieldReportId = report.Id,
                    ActorId = actorId
                })
                .ToList();
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [RequirePermission("FieldReports.Delete")]
    public async Task<IActionResult> DeleteFieldReport(int id)
    {
        var report = await _context.Set<FieldReport>().FindAsync(id);
        if (report == null) return NotFound();
        _context.Set<FieldReport>().Remove(report);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
