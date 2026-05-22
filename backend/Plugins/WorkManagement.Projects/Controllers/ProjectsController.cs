using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.WorkManagement.Projects.DTOs;
using Lidstroem.Plugins.WorkManagement.Projects.Entities;
using Lidstroem.Plugins.WorkManagement.Projects.Events;
using Lidstroem.Shared.Attributes;
using Lidstroem.Shared.Controllers.Base;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.WorkManagement.Projects.Controllers;

[Route("api/projects")]
[ApiController]
[Authorize]
public class ProjectsController : BaseLidstroemController<Project>
{
    public ProjectsController(
        DbContext context,
        IEnumerable<IEntityExtensionProvider> extenders,
        IPublisher publisher,
        IRealtimeNotifier realtime,
        ITenantContext tenantContext)
        : base(context, extenders, publisher, realtime, tenantContext) { }

    protected override Task MapDtoToEntity(object dto, Project entity)
    {
        var d = (ProjectDto)dto;
        entity.Title = d.Title;
        entity.Description = d.Description;
        return Task.CompletedTask;
    }

    protected override async Task OnAfterCreate(Project entity)
    {
        await _publisher.Publish(new ProjectCreatedEvent(entity.Id, entity.TenantId));
    }

    protected override Task OnBeforeDelete(Project entity)
    {
        entity.AddDomainEvent(new ProjectDeletedEvent(entity.Id, entity.TenantId));
        return Task.CompletedTask;
    }

    [HttpGet]
    [RequirePermission("Projects.View")]
    public async Task<ActionResult<IEnumerable<object>>> GetProjects(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        // Project list does not eager-load Members — use the detail endpoint for member data
        return Ok(await _context.Set<Project>()
            .OrderBy(p => p.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Title, p.Description, p.CreatedAt,
                MemberCount = p.Members.Count,
            })
            .ToListAsync());
    }

    [HttpGet("{id}")]
    [RequirePermission("Projects.View")]
    public async Task<ActionResult<object>> GetProject(int id)
    {
        var project = await _context.Set<Project>()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null) return NotFound();
        return Ok(await OkWithExtensions(project, id));
    }

    [HttpPost]
    [RequirePermission("Projects.Create")]
    public async Task<ActionResult<Project>> PostProject(ProjectDto dto) =>
        await PostGeneric(dto);

    [HttpPut("{id}")]
    [RequirePermission("Projects.Edit")]
    public async Task<IActionResult> PutProject(int id, ProjectDto dto)
    {
        if (id != dto.Id) return BadRequest();
        return await PutGeneric(id, dto);
    }

    [HttpDelete("{id}")]
    [RequirePermission("Projects.Delete")]
    public async Task<IActionResult> DeleteProject(int id) =>
        await DeleteGeneric(id);

    [HttpPost("{id}/members/{actorId}")]
    [RequirePermission("Projects.ManageMembers")]
    public async Task<IActionResult> AddMember(int id, int actorId)
    {
        var project = await _context.Set<Project>()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null) return NotFound("Project not found.");

        if (project.Members.Any(m => m.ActorId == actorId))
            return Conflict("Actor is already a member of this project.");

        project.Members.Add(new ProjectMember { ProjectId = id, ActorId = actorId });
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}/members/{actorId}")]
    [RequirePermission("Projects.ManageMembers")]
    public async Task<IActionResult> RemoveMember(int id, int actorId)
    {
        var project = await _context.Set<Project>()
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null) return NotFound("Project not found.");

        var member = project.Members.FirstOrDefault(m => m.ActorId == actorId);
        if (member == null) return NotFound("Actor is not a member of this project.");

        project.Members.Remove(member);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
