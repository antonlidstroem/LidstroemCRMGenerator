using Lidstroem.Core.GDPR;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.ACL.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.ACL;

public class AclPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("ACL.Grant",        "Share resources",      "Share a resource with another actor",          "ACL"),
        new PermissionDefinition("ACL.Revoke",       "Revoke sharing",       "Revoke a shared resource",                     "ACL"),
        // BUG FIX #2: ACL.ViewGrants is the canonical name used in AclController; ACL.View was
        // also referenced on the GetGrants endpoint but never declared — always returning 403.
        new PermissionDefinition("ACL.ViewGrants",   "View shares",          "List who has access",                          "ACL"),
        new PermissionDefinition("ACL.View",         "View ACL grants",      "Read access-control grant records",            "ACL"),
        // BUG FIX #3: ACL.ManageGrants was used in the admin-bypass check inside Grant/Revoke
        // but was never declared, so HasPermissionAsync always returned false and the bypass
        // could never be triggered even for super-admins.
        new PermissionDefinition("ACL.ManageGrants", "Manage all grants",    "Admin override — grant/revoke any resource",   "ACL"),
    };
}

public class AclPluginMetadata : IPluginMetadata
{
    public string PluginKey => "ACL";
    public string RoutePrefix => "acl";
}

public class AclGdprHandler : IGdprHandler
{
    private readonly DbContext _context;
    public string HandlerName => "ACL";

    public AclGdprHandler(DbContext context) => _context = context;

    public async Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId, string subjectType, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var grants = await _context.Set<AclGrant>().IgnoreQueryFilters()
                .Where(g => g.TenantId == tenantId
                         && (g.GrantedToActorId == subjectId || g.GrantedByActorId == subjectId))
                .ToListAsync(ct);

            _context.Set<AclGrant>().RemoveRange(grants);
            await _context.SaveChangesAsync(ct);
            return GdprHandlerResult.Ok(HandlerName, grants.Count);
        }
        catch (Exception ex)
        {
            return GdprHandlerResult.Failed(HandlerName, ex.Message);
        }
    }
}
