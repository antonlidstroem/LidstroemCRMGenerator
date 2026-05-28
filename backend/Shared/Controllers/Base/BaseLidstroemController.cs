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
        BroadcastFireAndForget(entity.Id, ChangeType.Created);
        return CreatedAtAction(GetCreatedAtActionName(), new { id = entity.Id }, entity);
    }

    protected async Task<IActionResult> PutGeneric(int id, object dto, T? existing = null)
    {
        var entity = existing ?? await _context.Set<T>().FindAsync(id);
        if (entity == null) return NotFound();
        await MapDtoToEntity(dto, entity);
        await OnBeforeUpdate(entity);
        await _context.SaveChangesAsync();
        BroadcastFireAndForget(id, ChangeType.Updated);
        return NoContent();
    }

    protected async Task<IActionResult> DeleteGeneric(int id)
    {
        var entity = await _context.Set<T>().FindAsync(id);
        if (entity == null) return NotFound();
        await OnBeforeDelete(entity);
        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
        BroadcastFireAndForget(id, ChangeType.Deleted);
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

    /// <summary>
    /// Generic paginated list endpoint. Sets X-Total-Count response header so
    /// the frontend can calculate TotalPages without fetching all records.
    ///
    /// POINT 1 FIX: EntityList.razor uses Api.LastTotalCount (read from this header)
    /// to drive server-side pagination. Without this header the frontend fell back
    /// to items.Count (always ≤ pageSize) and showed only one page even when more existed.
    ///
    /// Usage in a concrete controller:
    ///   [HttpGet]
    ///   public Task<ActionResult<IEnumerable<T>>> GetAll(
    ///       [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    ///       => GetGeneric(page, pageSize);
    /// </summary>
    protected async Task<ActionResult<IEnumerable<T>>> GetGeneric(
        int page = 1, int pageSize = 50,
        IQueryable<T>? query = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);

        var q = query ?? _context.Set<T>().AsQueryable();

        var totalCount = await q.CountAsync();

        // CORS: expose the header so browsers allow JS to read it cross-origin
        Response.Headers["X-Total-Count"]               = totalCount.ToString();
        Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(items);
    }

    // ── Realtime broadcast ────────────────────────────────────────────────────

    private void BroadcastFireAndForget(int entityId, ChangeType changeType)
    {
        // Intentional fire-and-forget: a broadcast failure must never affect the HTTP response.
        // We capture tenantId before the task runs so we don't access HttpContext after disposal.
        try
        {
            var tenantId = _tenantContext.TenantId;
            _ = _realtime
                .NotifyEntityChangedAsync(tenantId, typeof(T).Name, entityId, changeType)
                .ContinueWith(
                    t => { /* swallow — already logged inside SignalRRealtimeNotifier */ },
                    TaskContinuationOptions.OnlyOnFaulted);
        }
        catch
        {
            // Swallow any synchronous exception from TenantId resolution or notifier setup.
        }
    }
}
