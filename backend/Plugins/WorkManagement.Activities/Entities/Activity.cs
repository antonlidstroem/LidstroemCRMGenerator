using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.WorkManagement.Activities.Entities;

/// <summary>
/// An activity that MUST belong to exactly one Project.
/// ProjectId is a required FK — no navigation property to keep the plugin
/// decoupled from WorkManagement.Projects.
/// Involved actors are stored in a join table using ActorId only.
/// </summary>
public class Activity : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Required FK to the owning Project. Must be > 0.
    /// TargetType is always "Project" but stored as "Project" string
    /// in the LinkResolver so cross-plugin name lookups still work.
    /// </summary>
    public int ProjectId { get; set; }

    public List<ActivityActor> InvolvedActors { get; set; } = new();
}

/// <summary>
/// Join entity for Activity — Actor participants.
/// Uses ActorId directly — no navigation to Actor entity.
/// </summary>
public class ActivityActor
{
    public int ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public int ActorId { get; set; }
}
