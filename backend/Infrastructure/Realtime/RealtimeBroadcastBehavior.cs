using Lidstroem.Core.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Infrastructure.Realtime;

/// <summary>
/// MediatR pipeline behaviour that automatically broadcasts a realtime event
/// after any INotification that implements IRealtimeBroadcast.
///
/// Plugins opt in by implementing IRealtimeBroadcast on their domain events:
///
///   public record ProjectUpdatedEvent(Guid TenantId, int ProjectId)
///       : INotification, IRealtimeBroadcast
///   {
///       public string EntityType => "Project";
///       public int    EntityId   => ProjectId;
///       public ChangeType ChangeType => ChangeType.Updated;
///   }
///
/// No changes needed in the handler itself — broadcasting is automatic.
/// </summary>
public class RealtimeBroadcastBehavior<TNotification, TResponse>
    : IPipelineBehavior<TNotification, TResponse>
    where TNotification : IRequest<TResponse>
{
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<RealtimeBroadcastBehavior<TNotification, TResponse>> _logger;

    public RealtimeBroadcastBehavior(
        IRealtimeNotifier notifier,
        ILogger<RealtimeBroadcastBehavior<TNotification, TResponse>> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TNotification request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is IRealtimeBroadcast broadcast)
        {
            try
            {
                await _notifier.NotifyEntityChangedAsync(
                    broadcast.TenantId,
                    broadcast.EntityType,
                    broadcast.EntityId,
                    broadcast.ChangeType,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Broadcasting failure must never fail the original operation.
                _logger.LogWarning(ex,
                    "RealtimeBroadcastBehavior failed for {EntityType}/{EntityId}",
                    broadcast.EntityType, broadcast.EntityId);
            }
        }

        return response;
    }
}
