// ─────────────────────────────────────────────────────────────────────────────
// Lidstroem.CustomPages.ProjectDashboard
//
// Example custom page plugin. Copy this project as a starting point.
// Project type: Razor Class Library (.NET 8)
//
// Checklist to create a new custom page:
//   1. Copy this project, rename it and its namespace
//   2. Implement ICustomPageMetadata (see PageMetadata below)
//   3. Build your Razor component (see ProjectDashboardPage below)
//   4. Add <ProjectReference> in WebAPI.csproj
//   5. Add CustomPageRegistry.Register(...) in frontend Program.cs
//   6. Run: dotnet ef migrations add Add<Name>CustomPage (if you add DB tables)
// ─────────────────────────────────────────────────────────────────────────────

using Lidstroem.Core.Interfaces;

namespace Lidstroem.CustomPages.ProjectDashboard;

/// <summary>
/// Registers this custom page with the system.
/// Discovered at startup via assembly scanning — no manual registration needed in backend.
/// </summary>
public class ProjectDashboardPageMetadata : ICustomPageMetadata
{
    public string PageKey     => "ProjectDashboard";
    public string DisplayName => "Project dashboard";
    public string? Description => "Combined view of projects, milestones and linked donations.";

    // Replaces the generic /entity/Project list for tenants where this page is active.
    // Could also be a brand-new route like "/crm/projects".
    public string Route       => "/entity/Project";

    public string? NavGroup   => "Work Management";
    public int NavOrder        => 10;
    public string? Icon        => "layout-dashboard";
}
