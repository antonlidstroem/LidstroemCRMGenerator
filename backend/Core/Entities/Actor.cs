using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Core.Entities;

/// <summary>
/// The universal subject entity. Represents any agent that can authenticate,
/// own data, be assigned roles, and be targeted by GDPR operations.
/// Plugins extend Actor data via their own tables using ActorId as FK.
/// </summary>
public class Actor : BaseEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
