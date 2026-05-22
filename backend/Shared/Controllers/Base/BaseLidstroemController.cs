using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Shared.Controllers.Base;

public abstract class BaseLidstroemController<T> : ControllerBase
    where T : BaseEntity, new()
{
    protected readonly DbContext _context;
    protected readonly IEnumerable<IEntityExtensionProvider> _extenders;
    protected readonly IPublisher _publisher;
    protected readonly IRealtimeNotifier _realtime;
    protected readonly ITenantContext _tenantContext;

    protected BaseLidstroemController(
        DbContext context,
        IEnumerable<IEntityExtensionProvider> extenders,
        IPublisher publisher,
        IRealtimeNotifier realtime,
        ITenantContext tenantContext)
    {
        _context = context;
        _extenders = extenders;
        _publisher = publisher;
        _realtime = realtime;
        _tenantContext = tenantContext;
    }

    protected virtual Task OnBeforeCreate(T entity) => Task.CompletedTask;
    protected virtual Task OnAfterCreate(T entity) => Task.CompletedTask;
    protected virtual Task OnBeforeUpdate(T entity) => Task.CompletedTask;
    protected virtual Task OnBeforeDelete(T entity) => Task.CompletedTask;

    protected virtual Task MapDtoToEntity(object dto, T entity) =>
        throw new NotImplementedException($"{GetType().Name} must override MapDtoToEntity.");

    protected async Task<ActionResult<T>> PostGeneric(object dto)
    {
        var entity = new T();
        await MapDtoToEntity(dto, entity);
        await OnBeforeCreate(entity);
        _context.Set<T>().Add(entity);
        await _context.SaveChangesAsync();
        await OnAfterCreate(entity);
        await BroadcastAsync(entity.Id, ChangeType.Created);
        return CreatedAtAction(GetCreatedAtActionName(), new { id = entity.Id }, entity);
    }

    protected async Task<IActionResult> PutGeneric(int id, object dto, T? existing = null)
    {
        var entity = existing ?? await _context.Set<T>().FindAsync(id);
        if (entity == null) return NotFound();
        await MapDtoToEntity(dto, entity);
        await OnBeforeUpdate(entity);
        await _context.SaveChangesAsync();
        await BroadcastAsync(id, ChangeType.Updated);
        return NoContent();
    }

    protected async Task<IActionResult> DeleteGeneric(int id)
    {
        var entity = await _context.Set<T>().FindAsync(id);
        if (entity == null) return NotFound();
        await OnBeforeDelete(entity);
        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
        await BroadcastAsync(id, ChangeType.Deleted);
        return NoContent();
    }

    protected async Task<object> OkWithExtensions(T entity, int id)
    {
        var entityName = typeof(T).Name;
        var extensions = new Dictionary<string, object>();

        foreach (var ext in _extenders.Where(x => x.TargetEntityName == entityName))
        {
            var data = await ext.GetExtensionDataAsync(id, _context);
            if (data != null)
                extensions[ext.GetType().Assembly.GetName().Name!.Split('.').Last()] = data;
        }

        return new { Data = entity, Extensions = extensions };
    }

    protected virtual string GetCreatedAtActionName() => $"Get{typeof(T).Name}";

    // ── Realtime broadcast ────────────────────────────────────────────────────

    private Task BroadcastAsync(int entityId, ChangeType changeType)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            return _realtime.NotifyEntityChangedAsync(
                tenantId,
                typeof(T).Name,
                entityId,
                changeType);
        }
        catch
        {
            // Never let a broadcast failure affect the HTTP response.
            return Task.CompletedTask;
        }
    }
}
