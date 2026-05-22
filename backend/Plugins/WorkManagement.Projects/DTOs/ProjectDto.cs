// BUG FIX #7b: ProjectDto was missing [Required] on Title, so posting an empty title
// silently succeeded and the test PostProject_WithoutTitle_Returns400 would fail.
using System.ComponentModel.DataAnnotations;

namespace Lidstroem.Plugins.WorkManagement.Projects.DTOs;

public class ProjectDto
{
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
