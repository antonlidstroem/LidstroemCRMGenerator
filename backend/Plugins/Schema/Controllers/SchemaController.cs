using Lidstroem.Core.Auth;
using Lidstroem.Core.Interfaces;
using Lidstroem.Core.Schema;
using Lidstroem.Plugins.SuperAdmin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Schema.Controllers;

[Route("api/schema")]
[ApiController]
[Authorize]
public class SchemaController : ControllerBase
{
    private readonly SchemaRegistry _registry;
    private readonly IAuthContext   _auth;
    private readonly DbContext      _context;

    public SchemaController(SchemaRegistry registry, IAuthContext auth, DbContext context)
    {
        _registry = registry;
        _auth     = auth;
        _context  = context;
    }

    /// <summary>
    /// Returns schemas filtered to only include plugins that are active for
    /// the requesting tenant.
    ///
    /// POINT 2 FIX: Previous version used TenantExternalId directly on
    /// TenantPluginAssignment, but the entity uses TenantEntityId (int FK to
    /// Tenant.Id). The correct lookup chain is:
    ///   JWT tenantId claim (Guid = Tenant.ExternalId)
    ///     → find Tenant.Id (int)
    ///     → filter TenantPluginAssignment.TenantEntityId == Tenant.Id
    ///
    /// SuperAdmin sees all schemas regardless of plugin state — needed for
    /// TenantDetail plugin management and actor admin UIs.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyDictionary<string, EntitySchema>>> GetAll()
    {
        var all = _registry.GetAll();

        if (_auth.HasPermission("SuperAdmin.ManageTenants"))
            return Ok(all);

        var enabledKeys = await GetEnabledPluginKeysAsync();

        var filtered = all
            .Where(kvp =>
                string.IsNullOrEmpty(kvp.Value.OwnerPluginKey) ||
                enabledKeys.Contains(kvp.Value.OwnerPluginKey))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase);

        return Ok(filtered);
    }

    [HttpGet("{entityType}")]
    public async Task<ActionResult<EntitySchema>> Get(string entityType)
    {
        var schema = _registry.Get(entityType);
        if (schema == null) return NotFound($"No schema for '{entityType}'.");

        if (_auth.HasPermission("SuperAdmin.ManageTenants"))
            return Ok(schema);

        if (!string.IsNullOrEmpty(schema.OwnerPluginKey))
        {
            var enabledKeys = await GetEnabledPluginKeysAsync();
            if (!enabledKeys.Contains(schema.OwnerPluginKey))
                return Forbid();
        }

        return Ok(schema);
    }

    [HttpGet("types")]
    public ActionResult<IReadOnlyCollection<string>> GetTypes() =>
        Ok(_registry.GetRegisteredTypes());

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> GetEnabledPluginKeysAsync()
    {
        // _auth.TenantId is the Guid stored as Tenant.ExternalId in the DB.
        if (_auth.TenantId == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // POINT 2 FIX: Two-step lookup.
        // Step 1: resolve ExternalId (Guid) → Tenant.Id (int)
        var tenantId = await _context.Set<Tenant>()
            .IgnoreQueryFilters()
            .Where(t => t.ExternalId == _auth.TenantId.Value)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();

        if (tenantId == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Step 2: find enabled plugin keys for that tenant int id
        var keys = await _context.Set<TenantPluginAssignment>()
            .IgnoreQueryFilters()
            .Where(a => a.TenantEntityId == tenantId.Value && a.IsEnabled)
            .Select(a => a.PluginKey)
            .ToListAsync();

        return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
    }
}
