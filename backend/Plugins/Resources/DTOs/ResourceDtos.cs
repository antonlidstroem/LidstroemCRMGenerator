using System.ComponentModel.DataAnnotations;
using Lidstroem.Plugins.Resources.Entities;

namespace Lidstroem.Plugins.Resources.DTOs;

public class CreateLinkResourceDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(2000)]
    [Url(ErrorMessage = "ExternalUrl must be a valid absolute URL.")]
    public string ExternalUrl { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "TargetId must reference a valid entity.")]
    public int TargetId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(100)]
    public string TargetType { get; set; } = string.Empty;
}

public class ResourceResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ResourceType Type { get; set; }
    public int TargetId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string? PublicUrl { get; set; }
    public DateTime? UploadedAt { get; set; }
}
