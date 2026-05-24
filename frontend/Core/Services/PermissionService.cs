using System.Text.Json;
using Lidstroem.Frontend.Core.Auth;

namespace Lidstroem.Frontend.Core.Services;

public class PermissionService
{
    // Fix 11: Replaced raw HttpClient with ApiClient so requests include the
    // Authorization: Bearer header. Without this, the permissions endpoint
    // returns 401 and the HashSet stays null — Has() always returns false,
    // silently hiding all permission-gated UI elements for every user.
    private readonly ApiClient _api;
    private readonly AuthService _auth;
    private HashSet<string>? _permissions;

    public PermissionService(ApiClient api, AuthService auth)
    {
        _api  = api;
        _auth = auth;
    }

    public async Task LoadAsync()
    {
        if (_auth.ActorId == null || _auth.TenantId == null) return;

        // Use /my-permissions — a self-serve endpoint any authenticated user can call.
        // The previous endpoint (/actor/{id}/permissions) requires SuperAdmin.ManageTenants,
        // so non-admin users received 403 and _permissions stayed null, making Has() always
        // return false and hiding all permission-gated UI.
        var response = await _api.GetListAsync(
            $"/api/rbac/my-permissions?tenantId={_auth.TenantId}");

        _permissions = response != null
            ? new HashSet<string>(
                response.Select(el => el.GetString() ?? string.Empty)
                        .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();
    }

    public bool Has(string permission)
    {
        if (string.IsNullOrEmpty(permission)) return true;
        return _permissions?.Contains(permission) ?? false;
    }

    public void Clear() => _permissions = null;
}
