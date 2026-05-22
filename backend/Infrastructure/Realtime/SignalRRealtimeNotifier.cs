using Lidstroem.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Infrastructure.Realtime;

/// <summary>
/// IRealtimeNotifier implementation that sends messages via SignalR.
/// Registered as scoped — safe to inject into controllers and handlers.
///
/// Failure is non-fatal: if SignalR is unavailable (e.g. in tests or
/// during shutdown) a warning is logged and the call returns gracefully.
/// The caller's operation (saving to DB) has already succeeded by then.
/// </summary>
public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<LidstroemHub> _hub;
    private readonly ILogger<SignalRRealtimeNotifier> _logger;

    public SignalRRealtimeNotifier(
        IHubContext<LidstroemHub> hub,
        ILogger<SignalRRealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyEntityChangedAsync(
        Guid tenantId,
        string entityType,
        int entityId,
        ChangeType changeType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var group = $"tenant:{tenantId}";
            await _hub.Clients.Group(group).SendAsync(
                "EntityChanged",
                new { entityType, entityId, changeType = changeType.ToString() },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to broadcast EntityChanged for {EntityType}/{EntityId}",
                entityType, entityId);
        }
    }

    public async Task NotifyCustomAsync(
        Guid tenantId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var group = $"tenant:{tenantId}";
            await _hub.Clients.Group(group).SendAsync(
                "CustomEvent",
                new { eventName, payload },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to broadcast CustomEvent '{EventName}'", eventName);
        }
    }
}
