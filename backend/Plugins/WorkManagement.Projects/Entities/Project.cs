using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Events;

namespace Lidstroem.Plugins.WorkManagement.Projects.Entities;

/// <summary>
/// Project members are stored as a join table using ActorId only —
/// no navigation property to Actor to avoid cross-plugin coupling.
/// </summary>
public class Project : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ProjectMember> Members { get; set; } = new();
}

/// <summary>
/// Join entity for Project ↔ Actor members.
/// Uses ActorId directly — no navigation to Actor entity.
/// </summary>
public class ProjectMember
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int ActorId { get; set; }
}
