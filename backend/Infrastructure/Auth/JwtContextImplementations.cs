using System.IdentityModel.Tokens.Jwt;
using Lidstroem.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Lidstroem.Infrastructure.Auth;

public class JwtTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _http;

    public JwtTenantContext(IHttpContextAccessor http) => _http = http;

    public Guid TenantId
    {
        get
        {
            var claim = _http.HttpContext?.User.FindFirst("tenant_id")?.Value;
            // FIX #13: Return Guid.Empty instead of throwing for unauthenticated/anonymous requests.
            // Throwing here caused 500s on every [AllowAnonymous] endpoint that touched any
            // service that resolved ITenantContext (e.g. PluginActivationMiddleware).
            // Callers that genuinely require a tenant should assert IsAuthenticated themselves,
            // or the [Authorize] / [RequirePermission] attribute will reject the request first.
            if (claim != null && Guid.TryParse(claim, out var id)) return id;
            return Guid.Empty;
        }
    }

    public Guid? OwnerId
    {
        get
        {
            var claim = _http.HttpContext?.User.FindFirst("owner_id")?.Value;
            return claim != null && Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsSystemContext => false;
}

public class JwtCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _http;

    public JwtCurrentUserContext(IHttpContextAccessor http) => _http = http;

    public string? UserId =>
        _http.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    public string? DisplayName =>
        _http.HttpContext?.User.FindFirst("identifier")?.Value;
}
