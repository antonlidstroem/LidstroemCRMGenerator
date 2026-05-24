using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.SuperAdmin.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Lidstroem.Plugins.SuperAdmin.Middleware;

public class PluginActivationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    // Pre-built O(1) lookup: routePrefix (lowercase) → pluginKey
    private readonly Dictionary<string, string> _routeToPluginKey;

    public PluginActivationMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        IEnumerable<IPluginMetadata> pluginMetadata)
    {
        _next = next;
        _cache = cache;
        _routeToPluginKey = pluginMetadata
            .GroupBy(m => m.RoutePrefix.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().PluginKey,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var pluginKey = ResolvePluginKey(context.Request.Path);

        if (pluginKey != null)
        {
            var tenantContext = context.RequestServices.GetService<ITenantContext>();
            if (tenantContext != null && !tenantContext.IsSystemContext)
            {
                var isEnabled = await IsPluginEnabledAsync(
                    context.RequestServices, tenantContext.TenantId, pluginKey);

                if (!isEnabled)
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Error = $"Plugin '{pluginKey}' is not enabled for this tenant."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }

    private string? ResolvePluginKey(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments == null || segments.Length < 2) return null;
        if (!string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)) return null;

        return _routeToPluginKey.TryGetValue(segments[1], out var key) ? key : null;
    }

    private async Task<bool> IsPluginEnabledAsync(
        IServiceProvider services, Guid tenantId, string pluginKey)
    {
        var cacheKey = $"plugin:{tenantId}:{pluginKey}";
        if (_cache.TryGetValue(cacheKey, out bool cached)) return cached;

        var dbContext = services.GetRequiredService<DbContext>();
        var tenant = await dbContext.Set<Tenant>().IgnoreQueryFilters()
            .Include(t => t.PluginAssignments)
            .FirstOrDefaultAsync(t => t.ExternalId == tenantId);

        var enabled = tenant != null && tenant.IsActive &&
            (tenant.PluginAssignments.FirstOrDefault(a => a.PluginKey == pluginKey)?.IsEnabled ?? true);

        _cache.Set(cacheKey, enabled, TimeSpan.FromSeconds(60));
        return enabled;
    }
}
