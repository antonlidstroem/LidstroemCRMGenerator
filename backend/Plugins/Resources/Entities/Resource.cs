using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Interfaces;

namespace Lidstroem.Plugins.Resources.Entities;

public class Resource : BaseEntity, IPolymorphicTarget
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ResourceType Type { get; set; }
    public int TargetId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? StoragePath { get; set; }
    public string? ExternalUrl { get; set; }
    public int? UploadedByActorId { get; set; }
    public DateTime? UploadedAt { get; set; }
}

public enum ResourceType { Image = 1, Document = 2, Link = 3, Audio = 4 }
