using Lidstroem.Core.Interfaces;

namespace Lidstroem.Plugins.ACL.DTOs;

public class GrantAclDto
{
    public int ResourceId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public int GrantedToActorId { get; set; }
    public AclAction Action { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class RevokeAclDto
{
    public int ResourceId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public int GrantedToActorId { get; set; }
    public AclAction Action { get; set; }
}
