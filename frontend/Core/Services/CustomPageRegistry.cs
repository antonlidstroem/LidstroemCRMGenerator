using System.Text.Json;

namespace Lidstroem.Frontend.Core.Services;

/// <summary>
/// Loaded once at boot from /pub/site/{slug}/custom-pages.
/// Holds the active custom pages for the current tenant and maps
/// routes to page keys so Entity.razor can dispatch correctly.
///
/// Custom page Razor components register themselves here via
/// CustomPageRegistry.Register() — called from Program.cs in each
/// custom page plugin assembly.
/// </summary>
public class CustomPageRegistry
{
    // PageKey → Razor component type. Populated at startup by custom page assemblies.
    private static readonly Dictionary<string, Type> _components =
        new(StringComparer.OrdinalIgnoreCase);

    // Route → ActiveCustomPageEntry. Populated at boot from the API.
    private readonly Dictionary<string, ActiveCustomPageEntry> _byRoute =
        new(StringComparer.OrdinalIgnoreCase);

    // All active entries in nav order — used by layouts to build the menu.
    private readonly List<ActiveCustomPageEntry> _all = new();

    public IReadOnlyList<ActiveCustomPageEntry> All => _all;

    // ── Static registration (called from Program.cs before app boot) ──────────

    /// <summary>
    /// Called by each custom page plugin to map its PageKey to its component type.
    /// Example (in Program.cs of the host app):
    ///   CustomPageRegistry.Register("ProjectDashboard", typeof(ProjectDashboardPage));
    /// </summary>
    public static void Register(string pageKey, Type componentType) =>
        _components[pageKey] = componentType;

    // ── Instance: loaded at boot from backend ─────────────────────────────────

    public async Task LoadAsync(ApiClient api, string tenantSlug)
    {
        _byRoute.Clear();
        _all.Clear();

        var items = await api.GetListAsync($"/pub/site/{tenantSlug}/custom-pages");
        if (items == null) return;

        foreach (var item in items)
        {
            var pageKey     = GetString(item, "pageKey");
            var displayName = GetString(item, "displayName");
            var route       = GetString(item, "route");
            var navGroup    = GetNullableString(item, "navGroup");
            var navOrder    = item.TryGetProperty("navOrder", out var o) ? o.GetInt32() : 100;
            var icon        = GetNullableString(item, "icon");

            if (string.IsNullOrEmpty(pageKey) || string.IsNullOrEmpty(route)) continue;

            // Only register if the component type is known in this frontend assembly.
            // Unknown keys are silently skipped — allows backend to register pages
            // that haven't been deployed to this frontend yet.
            if (!_components.TryGetValue(pageKey, out var componentType)) continue;

            var entry = new ActiveCustomPageEntry(
                pageKey, displayName, route, navGroup, navOrder, icon, componentType);

            _byRoute[route] = entry;
            _all.Add(entry);
        }

        _all.Sort((a, b) =>
        {
            var g = string.Compare(a.NavGroup ?? "", b.NavGroup ?? "", StringComparison.Ordinal);
            return g != 0 ? g : a.NavOrder.CompareTo(b.NavOrder);
        });
    }

    /// <summary>
    /// Returns the active custom page for a given route, or null if none is registered.
    /// Called by Entity.razor and custom route pages to decide whether to render a
    /// custom component or fall back to the generic entity renderer.
    /// </summary>
    public ActiveCustomPageEntry? GetForRoute(string route) =>
        _byRoute.TryGetValue(route, out var entry) ? entry : null;

    public bool HasCustomPage(string route) => _byRoute.ContainsKey(route);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) ? p.GetString() ?? string.Empty : string.Empty;

    private static string? GetNullableString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) ? p.GetString() : null;
}

public record ActiveCustomPageEntry(
    string PageKey,
    string DisplayName,
    string Route,
    string? NavGroup,
    int NavOrder,
    string? Icon,
    Type ComponentType);
