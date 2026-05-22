// BUG FIX #7a: PageDto was missing [Required] on Title and Content. Tests
// PostPage_WithoutTitle_Returns400 expected HTTP 400 but got 201 because
// ASP.NET Core model validation only fires for annotated fields.
using System.ComponentModel.DataAnnotations;

namespace Lidstroem.Plugins.CMS.DTOs;

public class PageDto
{
    // Id is needed by PUT /cms/pages/{id} for the server-side id==dto.Id guard
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Slug { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? MetaDescription { get; set; }
}

public record PageSummaryDto(string Slug, string Title, string? MetaDescription, DateTime? PublishedAt);

public record PublicPageDto(
    string Title,
    string Content,
    string? MetaDescription,
    string? MetaKeywords,
    string? CanonicalUrl,
    DateTime? PublishedAt,
    string SiteName);
