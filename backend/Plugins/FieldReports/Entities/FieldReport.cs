using Lidstroem.Core.Constants;
using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.FieldReports.Entities;

/// <summary>
/// FieldReport uses polymorphic links for Activity and Context.
/// Author and Contributors are referenced by ActorId only — no navigation
/// properties to Actor to avoid cross-plugin coupling.
/// </summary>
public class FieldReport : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    // Author — FK to Actor by ID only (no navigation property)
    public int AuthorActorId { get; set; } = ActorConstants.AnonymousActorId;

    // Polymorphic link to an activity
    public int? ActivityId { get; set; }
    public string? ActivityType { get; set; }

    // Polymorphic link to context (e.g. a project)
    public int? ContextId { get; set; }
    public string? ContextType { get; set; }

    // Contributor actor IDs stored as a join table (managed separately)
    public List<FieldReportContributor> Contributors { get; set; } = new();
}

/// <summary>
/// Join entity for FieldReport ↔ Actor contributors.
/// Uses ActorId directly — no navigation to Actor entity.
/// </summary>
public class FieldReportContributor
{
    public int FieldReportId { get; set; }
    public FieldReport? FieldReport { get; set; }
    public int ActorId { get; set; }
}
