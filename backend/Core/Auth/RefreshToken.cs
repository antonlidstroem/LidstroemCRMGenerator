using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Core.Auth;

public class RefreshToken : BaseEntity
{
    public int ActorCredentialsId { get; set; }
    public ActorCredentials? Credentials { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}
