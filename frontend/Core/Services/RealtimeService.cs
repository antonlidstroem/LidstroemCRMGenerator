using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace Lidstroem.Frontend.Core.Services;

/// <summary>
/// Manages the SignalR connection to LidstroemHub.
/// Started after successful login, stopped on logout.
///
/// Components subscribe to EntityChanged or CustomEvent and receive
/// push notifications when data they're showing has been modified
/// by another user or session.
///
/// Usage in a Razor component:
///   @inject RealtimeService Realtime
///   @implements IDisposable
///
///   protected override void OnInitialized()
///   {
///       _sub = Realtime.OnEntityChanged("Project", HandleProjectChanged);
///   }
///
///   private async Task HandleProjectChanged(EntityChangedEvent e)
///   {
///       if (e.EntityId == _currentProjectId) await LoadAsync();
///   }
///
///   public void Dispose() => _sub?.Dispose();
/// </summary>
public class RealtimeService : IAsyncDisposable
{
    private readonly IConfiguration _config;

    // Token factory instead of AuthService — breaks the circular dependency:
    //   ApiClient → AuthService → RealtimeService → AuthService
    // AuthService registers this via Program.cs / DI setup.
    private Func<string?>? _tokenProvider;

    private HubConnection? _connection;

    // Subscribers keyed by entity type (null key = all types)
    private readonly List<EntityChangedSubscription> _entitySubscriptions = new();
    private readonly List<CustomEventSubscription> _customSubscriptions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RealtimeConnectionState State { get; private set; } = RealtimeConnectionState.Disconnected;
    public event Action? StateChanged;

    public RealtimeService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Called by AuthService immediately after it is constructed so
    /// RealtimeService can get the current access token without creating
    /// a circular constructor dependency.
    /// </summary>
    public void SetTokenProvider(Func<string?> tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    // ── Connection lifecycle ──────────────────────────────────────────────────

    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection != null) return;

            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7209";
            var hubUrl = $"{apiBase.TrimEnd('/')}/hubs/lidstroem";

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    // Pass JWT as query string — required for WebSocket auth
                    options.AccessTokenProvider = () =>
                        Task.FromResult(_tokenProvider?.Invoke());
                })
                .WithAutomaticReconnect(new[] {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            _connection.On<EntityChangedMessage>("EntityChanged", msg =>
            {
                var subs = _entitySubscriptions
                    .Where(s => s.EntityType == null ||
                                string.Equals(s.EntityType, msg.EntityType,
                                    StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var sub in subs)
                    _ = sub.Handler(new EntityChangedEvent(
                        msg.EntityType, msg.EntityId, msg.ChangeType));
            });

            _connection.On<CustomEventMessage>("CustomEvent", msg =>
            {
                var subs = _customSubscriptions
                    .Where(s => s.EventName == null ||
                                string.Equals(s.EventName, msg.EventName,
                                    StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var sub in subs)
                    _ = sub.Handler(new CustomEvent(msg.EventName, msg.Payload));
            });

            _connection.Reconnecting += _ =>
            {
                State = RealtimeConnectionState.Reconnecting;
                StateChanged?.Invoke();
                return Task.CompletedTask;
            };

            _connection.Reconnected += _ =>
            {
                State = RealtimeConnectionState.Connected;
                StateChanged?.Invoke();
                return Task.CompletedTask;
            };

            _connection.Closed += _ =>
            {
                State = RealtimeConnectionState.Disconnected;
                StateChanged?.Invoke();
                return Task.CompletedTask;
            };

            await _connection.StartAsync();
            State = RealtimeConnectionState.Connected;
            StateChanged?.Invoke();
        }
        catch
        {
            State = RealtimeConnectionState.Disconnected;
            StateChanged?.Invoke();
            _connection = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
        State = RealtimeConnectionState.Disconnected;
        StateChanged?.Invoke();
    }

    // ── Subscriptions ─────────────────────────────────────────────────────────

    /// <summary>
    /// Subscribe to entity changes for a specific entity type.
    /// Pass null for entityType to receive all entity changes.
    /// Returns an IDisposable — dispose it in the component's Dispose().
    /// </summary>
    public IDisposable OnEntityChanged(
        string? entityType,
        Func<EntityChangedEvent, Task> handler)
    {
        var sub = new EntityChangedSubscription(entityType, handler);
        _entitySubscriptions.Add(sub);
        return new Unsubscriber(() => _entitySubscriptions.Remove(sub));
    }

    /// <summary>
    /// Subscribe to custom events by event name.
    /// Pass null for eventName to receive all custom events.
    /// </summary>
    public IDisposable OnCustomEvent(
        string? eventName,
        Func<CustomEvent, Task> handler)
    {
        var sub = new CustomEventSubscription(eventName, handler);
        _customSubscriptions.Add(sub);
        return new Unsubscriber(() => _customSubscriptions.Remove(sub));
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    // ── Private types ─────────────────────────────────────────────────────────

    private record EntityChangedSubscription(
        string? EntityType,
        Func<EntityChangedEvent, Task> Handler);

    private record CustomEventSubscription(
        string? EventName,
        Func<CustomEvent, Task> Handler);

    private record EntityChangedMessage(string EntityType, int EntityId, string ChangeType);
    private record CustomEventMessage(string EventName, object? Payload);

    private class Unsubscriber : IDisposable
    {
        private readonly Action _remove;
        public Unsubscriber(Action remove) => _remove = remove;
        public void Dispose() => _remove();
    }
}

// ── Public event types ────────────────────────────────────────────────────────

public record EntityChangedEvent(string EntityType, int EntityId, string ChangeType);
public record CustomEvent(string EventName, object? Payload);

public enum RealtimeConnectionState
{
    Disconnected,
    Connected,
    Reconnecting
}
