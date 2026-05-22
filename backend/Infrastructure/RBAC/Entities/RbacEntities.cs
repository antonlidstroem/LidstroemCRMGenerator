using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Infrastructure.RBAC.Entities;

public class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<RolePermission> RolePermissions { get; set; } = new();
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
    public List<RolePermission> RolePermissions { get; set; } = new();
    public List<ActorRoleAssignment> ActorAssignments { get; set; } = new();
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

public class ActorRoleAssignment : BaseEntity
{
    public int ActorId { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public int? AssignedByActorId { get; set; }
}
