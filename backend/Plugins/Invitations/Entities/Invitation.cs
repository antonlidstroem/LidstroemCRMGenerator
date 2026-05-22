using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.Invitations.Entities;

public class Invitation : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public string? RoleName { get; set; }
    public int InvitedByActorId { get; set; }
    public int? AcceptedByActorId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsable => Status == InvitationStatus.Pending && !IsExpired;
}

public enum InvitationStatus { Pending = 1, Accepted = 2, Revoked = 3, Expired = 4 }
