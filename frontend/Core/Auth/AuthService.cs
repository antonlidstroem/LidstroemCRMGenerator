using System.Net.Http.Json;
using System.Text.Json;
using Lidstroem.Frontend.Core.Models;
using Lidstroem.Frontend.Core.Services;
using Microsoft.JSInterop;

namespace Lidstroem.Frontend.Core.Auth;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly RealtimeService _realtime;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _expiresAt;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _expiresAt;
    public int? ActorId { get; private set; }
    public Guid? TenantId { get; private set; }

    /// <summary>Exposed for SignalR AccessTokenProvider.</summary>
    public string? AccessToken => _accessToken;

    public event Action? AuthStateChanged;

    public AuthService(HttpClient http, IJSRuntime js, RealtimeService realtime)
    {
        _http = http;
        _js = js;
        _realtime = realtime;
        // Wire up the token provider here — avoids the circular constructor
        // dependency (ApiClient→AuthService→RealtimeService→AuthService).
        // RealtimeService has no reference back to AuthService; it only gets
        // a lightweight Func<string?> that reads the current token.
        realtime.SetTokenProvider(() => _accessToken);
    }

    public async Task InitializeAsync()
    {
        _refreshToken = await _js.InvokeAsync<string?>("sessionStorage.getItem", "lid_rt");
        var expStr    = await _js.InvokeAsync<string?>("sessionStorage.getItem", "lid_exp");

        if (DateTime.TryParse(expStr, out var exp))
            _expiresAt = exp;

        if (!string.IsNullOrEmpty(_refreshToken))
            await TryRefreshAsync();

        if (IsAuthenticated)
        {
            ParseClaims();
            await _realtime.StartAsync();
        }
    }

    public async Task<bool> LoginAsync(string identifier, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(identifier, password));

        if (!response.IsSuccessStatusCode) return false;

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (tokens == null) return false;

        await StoreTokensAsync(tokens);
        ParseClaims();
        await _realtime.StartAsync();
        AuthStateChanged?.Invoke();
        return true;
    }

    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(_refreshToken))
        {
            try
            {
                await _http.PostAsJsonAsync("/api/auth/logout",
                    new { RefreshToken = _refreshToken });
            }
            catch { /* best-effort */ }
        }

        await _realtime.StopAsync();
        await ClearTokensAsync();
        ActorId = null;
        TenantId = null;
        AuthStateChanged?.Invoke();
    }

    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        var response = await _http.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = _refreshToken });

        if (!response.IsSuccessStatusCode)
        {
            await ClearTokensAsync();
            return false;
        }

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (tokens == null) return false;

        await StoreTokensAsync(tokens);
        return true;
    }

    public string? GetAuthorizationHeader() =>
        IsAuthenticated ? $"Bearer {_accessToken}" : null;

    private async Task StoreTokensAsync(TokenResponse tokens)
    {
        _accessToken  = tokens.AccessToken;
        _refreshToken = tokens.RefreshToken;
        _expiresAt    = tokens.ExpiresAt;

        await _js.InvokeVoidAsync("sessionStorage.setItem", "lid_rt",  _refreshToken);
        await _js.InvokeVoidAsync("sessionStorage.setItem", "lid_exp", _expiresAt.ToString("O"));
    }

    private async Task ClearTokensAsync()
    {
        _accessToken  = null;
        _refreshToken = null;
        _expiresAt    = default;

        await _js.InvokeVoidAsync("sessionStorage.removeItem", "lid_rt");
        await _js.InvokeVoidAsync("sessionStorage.removeItem", "lid_exp");
    }

    private void ParseClaims()
    {
        if (string.IsNullOrEmpty(_accessToken)) return;

        try
        {
            var parts   = _accessToken.Split('.');
            if (parts.Length < 2) return;
            var payload = parts[1];
            var padded  = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json    = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var doc     = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("sub", out var sub))
                ActorId = int.TryParse(sub.GetString(), out var id) ? id : null;

            if (doc.RootElement.TryGetProperty("tenant_id", out var tid))
                TenantId = Guid.TryParse(tid.GetString(), out var g) ? g : null;
        }
        catch { /* malformed token */ }
    }
}
