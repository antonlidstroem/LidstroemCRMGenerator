using System.ComponentModel.DataAnnotations;
using Lidstroem.Plugins.Donations.Entities;

namespace Lidstroem.Plugins.Donations.DTOs;

public class DonationDto : IValidatableObject
{
    public int Id { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "SEK";

    public DateTime? DonationDate { get; set; }

    // Donor — must be an Actor
    [Range(1, int.MaxValue, ErrorMessage = "DonorId must be a valid actor ID.")]
    public int? DonorId { get; set; }
    public string? DonorType { get; set; }

    // Target — must be Project or Activity when provided
    [Range(1, int.MaxValue, ErrorMessage = "TargetId must reference a valid entity.")]
    public int? TargetId { get; set; }
    public string? TargetType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DonorType != null && !Donation.AllowedDonorTypes.Contains(DonorType))
            yield return new ValidationResult(
                $"DonorType must be one of: {string.Join(", ", Donation.AllowedDonorTypes)}.",
                new[] { nameof(DonorType) });

        if (TargetType != null && !Donation.AllowedTargetTypes.Contains(TargetType))
            yield return new ValidationResult(
                $"TargetType must be one of: {string.Join(", ", Donation.AllowedTargetTypes)}.",
                new[] { nameof(TargetType) });

        if (TargetId.HasValue && string.IsNullOrEmpty(TargetType))
            yield return new ValidationResult(
                "TargetType is required when TargetId is provided.",
                new[] { nameof(TargetType) });

        if (!string.IsNullOrEmpty(TargetType) && !TargetId.HasValue)
            yield return new ValidationResult(
                "TargetId is required when TargetType is provided.",
                new[] { nameof(TargetId) });
    }
}
