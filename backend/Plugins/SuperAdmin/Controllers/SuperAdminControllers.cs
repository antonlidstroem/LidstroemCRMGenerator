using Lidstroem.Core.Constants;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.SuperAdmin.DTOs;
using Lidstroem.Plugins.SuperAdmin.Entities;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lidstroem.Plugins.SuperAdmin.Controllers;

[Route("api/admin/tenants")]
[ApiController]
[Authorize]
[RequirePermission("SuperAdmin.ManageTenants")]
public class TenantsController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IEnumerable<ICustomPageMetadata> _customPages;

    public TenantsController(
        DbContext context,
        IMemoryCache cache,
        IEnumerable<ICustomPageMetadata> customPages)
    {
        _context = context;
        _cache = cache;
        _customPages = customPages;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tenant>>> GetTenants() =>
        Ok(await _context.Set<Tenant>().IgnoreQueryFilters()
            .Include(t => t.PluginAssignments)
            .Include(t => t.CustomPages)
            .OrderBy(t => t.Name)
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<Tenant>> GetTenant(int id)
    {
        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();
        return Ok(tenant);
    }

    [HttpPost]
    public async Task<ActionResult<Tenant>> CreateTenant(CreateTenantDto dto)
    {
        var tenant = new Tenant
        {
            ExternalId = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            ContactEmail = dto.ContactEmail,
            ActorQuota = dto.ActorQuota,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow,
            TenantId = TenantConstants.SystemTenantId
        };

        _context.Set<Tenant>().Add(tenant);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTenant(int id, UpdateTenantDto dto)
    {
        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();

        if (!string.Equals(tenant.Name, dto.Name, StringComparison.OrdinalIgnoreCase))
        {
            var nameExists = await _context.Set<Tenant>().IgnoreQueryFilters()
                .AnyAsync(t => t.Name == dto.Name && t.Id != id);
            if (nameExists)
                return Conflict(new { error = "A tenant with this name already exists." });
        }

        tenant.Name = dto.Name;
        tenant.Description = dto.Description;
        tenant.ContactEmail = dto.ContactEmail;
        tenant.ActorQuota = dto.ActorQuota;

        await _context.SaveChangesAsync();
        return Ok(tenant);
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();
        tenant.IsActive = true;
        tenant.ActivatedAt = DateTime.UtcNow;
        tenant.DeactivatedAt = null;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();
        tenant.IsActive = false;
        tenant.DeactivatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Plugin toggles ────────────────────────────────────────────────────────

    [HttpPut("{id}/plugins/{pluginKey}/enable")]
    public async Task<IActionResult> EnablePlugin(int id, string pluginKey)
    {
        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();
        await SetPluginEnabled(tenant, pluginKey, true);
        return NoContent();
    }

    [HttpPut("{id}/plugins/{pluginKey}/disable")]
    public async Task<IActionResult> DisablePlugin(int id, string pluginKey)
    {
        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();
        await SetPluginEnabled(tenant, pluginKey, false);
        return NoContent();
    }

    // ── Custom page toggles ───────────────────────────────────────────────────

    /// <summary>Returns all registered custom page metadata — used to build the toggle UI.</summary>
    [HttpGet("custom-pages/available")]
    public ActionResult<IEnumerable<CustomPageDto>> GetAvailableCustomPages() =>
        Ok(_customPages.Select(p => new CustomPageDto(
            p.PageKey, p.DisplayName, p.Description, p.Route, p.NavGroup, p.NavOrder, p.Icon)));

    [HttpPut("{id}/custom-pages/{pageKey}/enable")]
    public async Task<IActionResult> EnableCustomPage(int id, string pageKey)
    {
        if (!_customPages.Any(p => string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase)))
            return NotFound(new { error = $"Custom page '{pageKey}' is not registered." });

        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();

        await SetCustomPageEnabled(tenant, pageKey, true);
        InvalidateCustomPageCache(tenant.ExternalId);
        return NoContent();
    }

    [HttpPut("{id}/custom-pages/{pageKey}/disable")]
    public async Task<IActionResult> DisableCustomPage(int id, string pageKey)
    {
        var tenant = await GetTenantFull(id);
        if (tenant == null) return NotFound();

        await SetCustomPageEnabled(tenant, pageKey, false);
        InvalidateCustomPageCache(tenant.ExternalId);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SetPluginEnabled(Tenant tenant, string pluginKey, bool enabled)
    {
        var assignment = tenant.PluginAssignments.FirstOrDefault(a => a.PluginKey == pluginKey);
        if (assignment == null)
        {
            assignment = new TenantPluginAssignment
            {
                TenantEntityId = tenant.Id,
                PluginKey = pluginKey,
                TenantId = TenantConstants.SystemTenantId
            };
            _context.Set<TenantPluginAssignment>().Add(assignment);
        }

        assignment.IsEnabled = enabled;
        if (enabled) assignment.EnabledAt = DateTime.UtcNow;
        else assignment.DisabledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _cache.Remove($"plugin:{tenant.ExternalId}:{pluginKey}");
    }

    private async Task SetCustomPageEnabled(Tenant tenant, string pageKey, bool enabled)
    {
        var entry = tenant.CustomPages.FirstOrDefault(
            p => string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            entry = new TenantCustomPage
            {
                TenantEntityId = tenant.Id,
                PageKey = pageKey,
                TenantId = TenantConstants.SystemTenantId
            };
            _context.Set<TenantCustomPage>().Add(entry);
        }

        entry.IsEnabled = enabled;
        if (enabled) entry.EnabledAt = DateTime.UtcNow;
        else entry.DisabledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    private void InvalidateCustomPageCache(Guid tenantExternalId) =>
        _cache.Remove($"custom-pages:{tenantExternalId}");

    private async Task<Tenant?> GetTenantFull(int id) =>
        await _context.Set<Tenant>().IgnoreQueryFilters()
            .Include(t => t.PluginAssignments)
            .Include(t => t.CustomPages)
            .FirstOrDefaultAsync(t => t.Id == id);
}

// ── Public site endpoint (called by frontend at boot, no auth) ────────────────

[Route("pub/site")]
[ApiController]
public class PublicSiteController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IEnumerable<ICustomPageMetadata> _customPages;

    public PublicSiteController(
        DbContext context,
        IMemoryCache cache,
        IEnumerable<ICustomPageMetadata> customPages)
    {
        _context = context;
        _cache = cache;
        _customPages = customPages;
    }

    /// <summary>
    /// Returns the active custom pages for a tenant, including full metadata
    /// from the registered ICustomPageMetadata implementations.
    /// Cached per tenant — invalidated when SuperAdmin toggles a page.
    /// </summary>
    [HttpGet("{slug}/custom-pages")]
    public async Task<ActionResult<IEnumerable<ActiveCustomPageDto>>> GetCustomPages(string slug)
    {
        var tenant = await _context.Set<Tenant>().IgnoreQueryFilters()
            .Include(t => t.CustomPages)
            .FirstOrDefaultAsync(t => t.Name == slug || t.ExternalId.ToString() == slug);

        if (tenant == null) return Ok(Array.Empty<ActiveCustomPageDto>());

        var cacheKey = $"custom-pages:{tenant.ExternalId}";
        if (_cache.TryGetValue(cacheKey, out List<ActiveCustomPageDto>? cached) && cached != null)
            return Ok(cached);

        var enabledKeys = tenant.CustomPages
            .Where(p => p.IsEnabled)
            .Select(p => p.PageKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = _customPages
            .Where(p => enabledKeys.Contains(p.PageKey))
            .Select(p => new ActiveCustomPageDto(
                p.PageKey, p.DisplayName, p.Route,
                p.NavGroup, p.NavOrder, p.Icon))
            .OrderBy(p => p.NavGroup)
            .ThenBy(p => p.NavOrder)
            .ToList();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return Ok(result);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CustomPageDto(
    string PageKey,
    string DisplayName,
    string? Description,
    string Route,
    string? NavGroup,
    int NavOrder,
    string? Icon);

/// <summary>Slim version sent to the frontend at boot — no description needed.</summary>
public record ActiveCustomPageDto(
    string PageKey,
    string DisplayName,
    string Route,
    string? NavGroup,
    int NavOrder,
    string? Icon);

[Route("api/admin/health")]
[ApiController]
[Authorize]
[RequirePermission("SuperAdmin.ViewHealth")]
public class SystemHealthController : ControllerBase
{
    private readonly DbContext _context;

    public SystemHealthController(DbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<SystemHealthDto>> GetHealth()
    {
        var canConnect = await _context.Database.CanConnectAsync();
        var tenantCount = await _context.Set<Tenant>().IgnoreQueryFilters().CountAsync();
        var activeTenantCount = await _context.Set<Tenant>().IgnoreQueryFilters().CountAsync(t => t.IsActive);
        var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync()).ToList();

        return Ok(new SystemHealthDto
        {
            DatabaseConnected = canConnect,
            TenantCount = tenantCount,
            ActiveTenantCount = activeTenantCount,
            PendingMigrations = pendingMigrations,
            HasPendingMigrations = pendingMigrations.Count > 0,
            ServerTime = DateTime.UtcNow
        });
    }

    [HttpGet("logs")]
    public async Task<ActionResult<IEnumerable<SystemLog>>> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        const int MaxPageSize = 200;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        page = Math.Max(1, page);

        var query = _context.Set<SystemLog>().IgnoreQueryFilters().AsQueryable();
        if (!string.IsNullOrEmpty(level)) query = query.Where(l => l.Level == level);
        return Ok(await query.OrderByDescending(l => l.LoggedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }
}

public class SystemHealthDto
{
    public bool DatabaseConnected { get; set; }
    public int TenantCount { get; set; }
    public int ActiveTenantCount { get; set; }
    public IList<string> PendingMigrations { get; set; } = new List<string>();
    public bool HasPendingMigrations { get; set; }
    public DateTime ServerTime { get; set; }
}
