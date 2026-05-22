// BUG FIX #7d: FieldReportDto was missing [Required] on Title and Content.
// Test PostFieldReport_WithoutTitle_Returns400 expected HTTP 400 but got 201.
using System.ComponentModel.DataAnnotations;

namespace Lidstroem.Plugins.FieldReports.DTOs;

public class FieldReportDto
{
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Content { get; set; } = string.Empty;

    public int AuthorActorId { get; set; }
    public int? ActivityId { get; set; }
    public string? ActivityType { get; set; }
    public int? ContextId { get; set; }
    public string? ContextType { get; set; }
    public List<int>? ContributorActorIds { get; set; }
}
