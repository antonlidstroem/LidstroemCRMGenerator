using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.CMS.Entities;

public class Page : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? AuthorActorId { get; set; }
    public int SortOrder { get; set; }
    public string? CanonicalUrl { get; set; }
}

public class TenantSite : BaseEntity
{
    public string Slug { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string? CustomDomain { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }

    // Theming
    public string ThemeName { get; set; } = "Clarity";
    public string SkinPackage { get; set; } = "Slate";
    public string? SkinJson { get; set; }
    public string DarkMode { get; set; } = "system"; // "light" | "dark" | "system"

    public bool IsPublic { get; set; } = true;
}
