using System.IdentityModel.Tokens.Jwt;
using Lidstroem.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Lidstroem.Infrastructure.Auth;

/// <summary>
/// HTTP-scoped implementation of IAuthContext.
/// Resolves tenant identity and performs synchronous permission checks
/// against the JWT claims loaded by the authentication middleware.
///
/// Permission checks are intentionally synchronous — they read from the
/// claims principal which is already populated by the time a controller
/// action executes. For async permission checks use IPermissionService directly.
/// </summary>
public class JwtAuthContext : IAuthContext
{
    private readonly IHttpContextAccessor _http;
    private readonly IPermissionService   _permissions;

    // Lazily resolved permission set — cached for the duration of the request.
    private IReadOnlyCollection<string>? _cachedPermissions;

    public JwtAuthContext(IHttpContextAccessor http, IPermissionService permissions)
    {
        _http        = http;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public Guid? TenantId
    {
        get
        {
            var claim = _http.HttpContext?.User.FindFirst("tenant_id")?.Value;
            return claim != null && Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Blocks synchronously on the first call per request to load permissions,
    /// then uses the cached set for subsequent calls.  This is safe because:
    ///   1. The permissions query is fast (memory-cached in PermissionService).
    ///   2. Controllers only call HasPermission a small number of times per request.
    /// </remarks>
    public bool HasPermission(string permission)
    {
        if (_cachedPermissions == null)
            _cachedPermissions = LoadPermissionsSync();

        return _cachedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private IReadOnlyCollection<string> LoadPermissionsSync()
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return Array.Empty<string>();

        var actorIdClaim  = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var tenantIdClaim = user.FindFirst("tenant_id")?.Value;

        if (!int.TryParse(actorIdClaim, out var actorId)
         || !Guid.TryParse(tenantIdClaim, out var tenantId))
            return Array.Empty<string>();

        // Run async method synchronously — safe here because PermissionService
        // is backed by an in-memory cache (no async I/O on cache hits).
        return _permissions.GetPermissionsAsync(actorId, tenantId)
            .GetAwaiter().GetResult();
    }
}
