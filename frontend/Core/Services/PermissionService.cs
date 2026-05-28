using System.Text.Json;
using Lidstroem.Frontend.Core.Auth;

namespace Lidstroem.Frontend.Core.Services;

public class PermissionService
{
    private readonly ApiClient  _api;
    private readonly AuthService _auth;
    private HashSet<string>? _permissions;

    private static readonly Guid SystemTenantId =
        new("00000000-0000-0000-0000-000000000001");

    // BUG-31 FIX: Expose a PermissionsLoaded event and IsLoaded flag so that
    // callers (Login.razor) can await the completion of LoadAsync without using
    // a magic Task.Delay constant that fails on slow backends.
    public event Action? PermissionsLoaded;
    public bool IsLoaded => _permissions != null;

    public PermissionService(ApiClient api, AuthService auth)
    {
        _api  = api;
        _auth = auth;
    }

    public async Task LoadAsync()
    {
        if (_auth.ActorId == null) return;

        var tenantId = _auth.TenantId ?? SystemTenantId;

        var response = await _api.GetListAsync(
            $"/api/rbac/my-permissions?tenantId={tenantId}");

        _permissions = response != null
            ? new HashSet<string>(
                response.Select(el => el.GetString() ?? string.Empty)
                        .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();

        // Notify subscribers (e.g. Login.razor waiting for permission check)
        PermissionsLoaded?.Invoke();
    }

    public bool Has(string permission)
    {
        if (string.IsNullOrEmpty(permission)) return true;
        return _permissions?.Contains(permission) ?? false;
    }

    public void Clear()
    {
        _permissions = null;
        // Reset IsLoaded so Login.razor doesn't see stale state after logout
        PermissionsLoaded = null;
    }
}
