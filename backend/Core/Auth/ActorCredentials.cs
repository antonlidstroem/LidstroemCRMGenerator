using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Core.Auth;

/// <summary>
/// Stores login credentials for an Actor. One Actor may have one credential record per tenant.
/// </summary>
public class ActorCredentials : BaseEntity
{
    public int ActorId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}
