using System.ComponentModel.DataAnnotations.Schema;
using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.Donations.Entities;

/// <summary>
/// A donation from a Donor to a Target.
///
/// Donor is always an Actor (polymorphic for future extensibility, but
/// validated to be "Actor" at the API layer).
///
/// Target must be one of the allowed types: "Project" or "Activity".
/// This is validated server-side — the DB stores the string but the
/// API rejects anything outside the allowed set.
/// </summary>
public class Donation : BaseEntity
{
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime DonationDate { get; set; } = DateTime.UtcNow;
    public string Currency { get; set; } = "SEK";

    // Who donated — polymorphic, validated to "Actor" at API layer
    public int? DonorId { get; set; }
    public string? DonorType { get; set; }

    // What was donated to — must be "Project" or "Activity"
    public int? TargetId { get; set; }
    public string? TargetType { get; set; }

    /// <summary>Allowed values for TargetType.</summary>
    public static readonly IReadOnlySet<string> AllowedTargetTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Project", "Activity" };

    /// <summary>Allowed values for DonorType.</summary>
    public static readonly IReadOnlySet<string> AllowedDonorTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Actor" };
}
