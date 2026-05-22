using System.ComponentModel.DataAnnotations;

namespace Lidstroem.Plugins.WorkManagement.Activities.DTOs;

public class ActivityDto
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Required — every activity must belong to a project.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "A valid ProjectId is required.")]
    public int ProjectId { get; set; }

    public List<int>? InvolvedActorIds { get; set; }
}
