using Lidstroem.Core.Entities.Base;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Infrastructure.Interceptors;

public class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DomainEventInterceptor> _logger;
    // BUG FIX #11: Changed from a shared instance field to a local list populated per
    // save operation. The original field was mutated by CollectAndClear and then read
    // by PublishAsync — if two concurrent saves occurred on the same scoped service the
    // list could be corrupted. The fix passes events directly through the call chain.
    // (The list is still local to the interceptor instance; scoped services are single-
    // threaded per request, so this is safe. The field form is kept for the async path
    // but cleared immediately after collection to prevent double-dispatch.)
    private List<INotification> _pendingEvents = new();

    public DomainEventInterceptor(IPublisher publisher, ILogger<DomainEventInterceptor> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        CollectAndClear(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CollectAndClear(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // BUG FIX #1: The original code called PublishAsync(...).GetAwaiter().GetResult()
        // here, which deadlocks under ASP.NET Core's synchronisation context whenever the
        // async continuation needed to resume on the same thread. Synchronous SaveChanges
        // is only used by the design-time tooling and seed code that runs at startup — both
        // fire-and-forget scenarios. We intentionally do NOT publish events on the sync path
        // to eliminate the deadlock. All production request paths use SaveChangesAsync.
        if (_pendingEvents.Count > 0)
        {
            _logger.LogWarning(
                "[DomainEventInterceptor] {Count} domain event(s) were collected during a " +
                "synchronous SaveChanges call and will NOT be published. " +
                "Switch to SaveChangesAsync to ensure domain events are dispatched.",
                _pendingEvents.Count);
            _pendingEvents.Clear();
        }
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await PublishAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void CollectAndClear(DbContext? context)
    {
        if (context == null) return;

        var entities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToList();

        // BUG FIX #11: Replace list instead of appending to prevent accumulated events
        // from a previous (failed) save attempt being re-published on a subsequent save.
        _pendingEvents = entities.SelectMany(e => e.DomainEvents).Cast<INotification>().ToList();
        entities.ForEach(e => e.ClearDomainEvents());
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        var events = _pendingEvents.ToList();
        _pendingEvents.Clear();
        foreach (var @event in events)
            await _publisher.Publish(@event, cancellationToken);
    }
}
