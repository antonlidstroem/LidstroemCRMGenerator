using Lidstroem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Lidstroem.Infrastructure.Realtime;

/// <summary>
/// Main SignalR hub. All connected clients join a tenant-scoped group on
/// connect so broadcasts are automatically isolated per tenant.
///
/// Clients never call hub methods directly — the hub is push-only.
/// All messages are sent from IRealtimeNotifier (server-side).
///
/// Message contract (client receives):
///
///   "EntityChanged" → { entityType, entityId, changeType }
///   "CustomEvent"   → { eventName, payload }
/// </summary>
[Authorize]
public class LidstroemHub : Hub
{
    private readonly ITenantContext _tenantContext;

    public LidstroemHub(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task OnConnectedAsync()
    {
        // Each connection joins a group named after the tenant's GUID.
        // Broadcasts target the group — no client ever receives another tenant's events.
        var tenantGroup = GetTenantGroup();
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantGroup = GetTenantGroup();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenantGroup);
        await base.OnDisconnectedAsync(exception);
    }

    private string GetTenantGroup()
    {
        try
        {
            return $"tenant:{_tenantContext.TenantId}";
        }
        catch
        {
            // Fallback for system/unauthenticated contexts — should not normally
            // reach the hub since [Authorize] blocks unauthenticated connections.
            return $"tenant:unknown:{Context.ConnectionId}";
        }
    }
}
