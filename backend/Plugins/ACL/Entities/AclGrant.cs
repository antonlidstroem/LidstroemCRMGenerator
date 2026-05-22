using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Interfaces;

namespace Lidstroem.Plugins.ACL.Entities;

public class AclGrant : BaseEntity
{
    public int ResourceId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public int GrantedByActorId { get; set; }
    public int GrantedToActorId { get; set; }
    public AclAction Action { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
