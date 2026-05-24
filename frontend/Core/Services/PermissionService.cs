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

    // The SuperAdmin actor lives in the system tenant (all-zeros GUID + 1).
    // When tenant_id is absent from the JWT (superadmin has no normal tenant),
    // we fall back to this sentinel so the permissions query still fires.
    private static readonly Guid SystemTenantId = new("00000000-0000-0000-0000-000000000001");

    public PermissionService(ApiClient api, AuthService auth)
    {
        _api = api;
        _auth = auth;
    }

    public async Task LoadAsync()
    {
        if (_auth.ActorId == null) return;

        // FIX: SuperAdmin actors are seeded with TenantId = SystemTenantId but
        // their JWT may carry no tenant_id claim (or an empty one), causing
        // _auth.TenantId to be null.  In that case fall back to SystemTenantId
        // so the permissions query always fires — previously the early-return
        // here left _permissions null, making Has() always return false and
        // hiding all permission-gated UI (including the Tenants admin link)
        // for every superadmin login.
        var tenantId = _auth.TenantId ?? SystemTenantId;

        // Use /my-permissions — a self-serve endpoint any authenticated user can call.
        // The previous endpoint (/actor/{id}/permissions) requires SuperAdmin.ManageTenants,
        // so non-admin users received 403 and _permissions stayed null, making Has() always
        // return false and hiding all permission-gated UI.
        var response = await _api.GetListAsync(
            $"/api/rbac/my-permissions?tenantId={tenantId}");

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