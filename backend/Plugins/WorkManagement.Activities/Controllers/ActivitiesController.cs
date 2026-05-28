using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.WorkManagement.Activities.DTOs;
using Lidstroem.Plugins.WorkManagement.Activities.Entities;
using Lidstroem.Shared.Attributes;
using Lidstroem.Shared.Controllers.Base;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.WorkManagement.Activities.Controllers;

[Route("api/activities")]
[ApiController]
[Authorize]
public class ActivitiesController : BaseLidstroemController<Activity>
{
    private readonly ILinkResolverService _linkResolver;

    public ActivitiesController(
        DbContext context,
        IEnumerable<IEntityExtensionProvider> extenders,
        IPublisher publisher,
        IRealtimeNotifier realtime,
        ITenantContext tenantContext,
        ILinkResolverService linkResolver)
        : base(context, extenders, publisher, realtime, tenantContext)
    {
        _linkResolver = linkResolver;
    }

    protected override Task MapDtoToEntity(object dto, Activity entity)
    {
        var d = (ActivityDto)dto;
        entity.Title = d.Title;
        entity.Description = d.Description;
        entity.StartDate = d.StartDate;
        entity.EndDate = d.EndDate;
        entity.ProjectId = d.ProjectId;
        return Task.CompletedTask;
    }

    [HttpGet]
    [RequirePermission("Activities.View")]
    public async Task<ActionResult<IEnumerable<object>>> GetActivities(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);

        var query = _context.Set<Activity>().OrderByDescending(a => a.StartDate);

        // POINT 1 FIX: Set X-Total-Count so EntityList.razor can calculate TotalPages
        // without fetching all records. Also expose the header for cross-origin CORS.
        var totalCount = await query.CountAsync();
        Response.Headers["X-Total-Count"]                = totalCount.ToString();
        Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.Title, a.Description, a.StartDate, a.EndDate, a.ProjectId,
                InvolvedActorCount = a.InvolvedActors.Count,
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("by-project/{projectId:int}")]
    [RequirePermission("Activities.View")]
    public async Task<ActionResult<IEnumerable<object>>> GetByProject(int projectId) =>
        Ok(await _context.Set<Activity>()
            .Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.StartDate)
            .Select(a => new
            {
                a.Id, a.Title, a.Description, a.StartDate, a.EndDate, a.ProjectId,
                InvolvedActorCount = a.InvolvedActors.Count,
            })
            .ToListAsync());

    [HttpGet("{id:int}")]
    [RequirePermission("Activities.View")]
    public async Task<ActionResult<object>> GetActivity(int id)
    {
        var activity = await _context.Set<Activity>()
            .Include(a => a.InvolvedActors)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null) return NotFound();

        var projectName = await _linkResolver.ResolveAsync(activity.ProjectId, "Project");
        var ext = await OkWithExtensions(activity, id);
        return Ok(new { Data = ext, ProjectName = projectName });
    }

    [HttpPost]
    [RequirePermission("Activities.Create")]
    public async Task<ActionResult<Activity>> PostActivity(ActivityDto dto)
    {
        if (dto.ProjectId <= 0)
            return BadRequest("A valid ProjectId is required.");

        var activity = new Activity
        {
            Title = dto.Title,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ProjectId = dto.ProjectId
        };

        if (dto.InvolvedActorIds?.Count > 0)
        {
            activity.InvolvedActors = dto.InvolvedActorIds
                .Select(actorId => new ActivityActor { ActorId = actorId })
                .ToList();
        }

        _context.Set<Activity>().Add(activity);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, activity);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("Activities.Edit")]
    public async Task<IActionResult> PutActivity(int id, ActivityDto dto)
    {
        if (id != dto.Id) return BadRequest();
        if (dto.ProjectId <= 0) return BadRequest("A valid ProjectId is required.");

        var activity = await _context.Set<Activity>()
            .Include(a => a.InvolvedActors)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null) return NotFound();

        activity.Title = dto.Title;
        activity.Description = dto.Description;
        activity.StartDate = dto.StartDate;
        activity.EndDate = dto.EndDate;
        activity.ProjectId = dto.ProjectId;

        if (dto.InvolvedActorIds != null)
        {
            activity.InvolvedActors.Clear();
            activity.InvolvedActors = dto.InvolvedActorIds
                .Select(actorId => new ActivityActor
                {
                    ActivityId = activity.Id,
                    ActorId = actorId
                })
                .ToList();
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("Activities.Delete")]
    public async Task<IActionResult> DeleteActivity(int id) =>
        await DeleteGeneric(id);
}
