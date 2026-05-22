using System.ComponentModel.DataAnnotations;

namespace Lidstroem.Plugins.Invitations.DTOs;

public class SendInvitationDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(320)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    public string? RoleName { get; set; }
}

public class AcceptInvitationDto
{
    [Required(AllowEmptyStrings = false)]
    public string Token { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;
}
