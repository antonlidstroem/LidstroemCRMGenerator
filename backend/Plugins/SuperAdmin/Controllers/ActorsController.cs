// BUG FIX #7c: ActorDto was missing [Required] on DisplayName and Email.
// Tests PostActor_WithoutDisplayName_Returns400 and PostActor_WithoutEmail_Returns400
// expected HTTP 400 but silently got 201 with empty strings stored in the DB.
using System.ComponentModel.DataAnnotations;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Events;
using Lidstroem.Core.Interfaces;
using Lidstroem.Shared.Attributes;
using Lidstroem.Shared.Controllers.Base;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.SuperAdmin.Controllers;

public class ActorDto
{
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
}

public class ActorPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("Actors.View",   "View actors",   "List and read actor records", "Actors"),
        new PermissionDefinition("Actors.Create", "Create actor",  "Add new actor",               "Actors"),
        new PermissionDefinition("Actors.Edit",   "Edit actor",    "Update actor details",        "Actors"),
        new PermissionDefinition("Actors.Delete", "Delete actor",  "Remove actor permanently",    "Actors"),
    };
}

[Route("api/actors")]
[ApiController]
[Authorize]
public class ActorsController : BaseLidstroemController<Actor>
{
    public ActorsController(
        DbContext context,
        IEnumerable<IEntityExtensionProvider> extenders,
        IPublisher publisher,
        IRealtimeNotifier realtime,
        ITenantContext tenantContext)
        : base(context, extenders, publisher, realtime, tenantContext) { }

    protected override Task MapDtoToEntity(object dto, Actor entity)
    {
        var d = (ActorDto)dto;
        entity.DisplayName = d.DisplayName;
        entity.Email = d.Email;
        entity.PhoneNumber = d.PhoneNumber;
        return Task.CompletedTask;
    }

    protected override async Task OnAfterCreate(Actor entity)
    {
        await _publisher.Publish(new ActorCreatedEvent(entity.Id, entity.TenantId));
    }

    protected override Task OnBeforeDelete(Actor entity)
    {
        entity.AddDomainEvent(new ActorDeletedEvent(entity.Id, entity.TenantId));
        return Task.CompletedTask;
    }

    [HttpGet]
    [RequirePermission("Actors.View")]
    public async Task<ActionResult<IEnumerable<Actor>>> GetActors(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        return Ok(await _context.Set<Actor>()
            .OrderBy(a => a.DisplayName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync());
    }

    [HttpGet("{id}")]
    [RequirePermission("Actors.View")]
    public async Task<ActionResult<object>> GetActor(int id)
    {
        var actor = await _context.Set<Actor>().FirstOrDefaultAsync(a => a.Id == id);
        if (actor == null) return NotFound();
        return Ok(await OkWithExtensions(actor, id));
    }

    [HttpPost]
    [RequirePermission("Actors.Create")]
    public async Task<ActionResult<Actor>> PostActor(ActorDto dto) =>
        await PostGeneric(dto);

    [HttpPut("{id}")]
    [RequirePermission("Actors.Edit")]
    public async Task<IActionResult> PutActor(int id, ActorDto dto)
    {
        if (id != dto.Id) return BadRequest();
        var actor = await _context.Set<Actor>().FindAsync(id);
        if (actor == null) return NotFound();
        actor.AddDomainEvent(new ActorUpdatedEvent(actor.Id, actor.TenantId));
        return await PutGeneric(id, dto, actor);
    }

    [HttpDelete("{id}")]
    [RequirePermission("Actors.Delete")]
    public async Task<IActionResult> DeleteActor(int id) =>
        await DeleteGeneric(id);
}
