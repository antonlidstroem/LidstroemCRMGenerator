using System.ComponentModel.DataAnnotations;

namespace Lidstroem.Plugins.SuperAdmin.DTOs;

public class CreateTenantDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(320)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [Range(1, 100000)]
    public int? ActorQuota { get; set; }
}

public class UpdateTenantDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(320)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [Range(1, 100000)]
    public int? ActorQuota { get; set; }
}
