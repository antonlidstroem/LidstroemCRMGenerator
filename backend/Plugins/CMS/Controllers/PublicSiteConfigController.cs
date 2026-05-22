using Lidstroem.Plugins.CMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lidstroem.Plugins.CMS.Controllers;

/// <summary>
/// Returns site configuration consumed by the Blazor frontend before authentication.
/// This is intentionally public — it contains only branding/theme data.
/// </summary>
[Route("pub/site")]
[ApiController]
[AllowAnonymous]
public class PublicSiteConfigController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IMemoryCache _cache;

    public PublicSiteConfigController(DbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<SiteConfigDto>> GetSiteConfig(string slug)
    {
        var cacheKey = $"siteconfig:{slug}";
        if (!_cache.TryGetValue(cacheKey, out SiteConfigDto? dto))
        {
            var site = await _context.Set<TenantSite>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Slug == slug && s.IsPublic);

            if (site == null) return NotFound();

            dto = new SiteConfigDto(
                site.SiteName,
                site.LogoUrl,
                site.FaviconUrl,
                site.ThemeName,
                site.SkinPackage,
                site.SkinJson,
                site.DarkMode);

            _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(60));
        }

        return Ok(dto);
    }
    /// <summary>
    /// Resolves a custom domain (e.g. "www.kund-a.se") to the matching tenant slug.
    /// Used by the Blazor frontend when no subdomain is detected.
    /// Returns 404 if no TenantSite has that CustomDomain.
    /// </summary>
    [HttpGet("by-host/{host}")]
    public async Task<ActionResult<SlugResponseDto>> GetSlugByHost(string host)
    {
        // BUG FIX #9a: The original code used chained TrimStart('w') calls to strip "www."
        // which is wrong — TrimStart trims individual characters, not substrings. Calling
        // .TrimStart('w') on "www.kund-a.se" strips all leading 'w' chars giving ".kund-a.se",
        // and .TrimStart('.') then produces "kund-a.se" — accidentally correct for exactly
        // three 'w's, but "ww2.example.com" would become "2.example.com".
        // BUG FIX #9b: The computed `normalised` variable was never used in the DB query;
        // the query still used host.ToLowerInvariant() directly, defeating the purpose.
        var normalised = host.ToLowerInvariant();
        if (normalised.StartsWith("www.", StringComparison.Ordinal))
            normalised = normalised[4..];

        var cacheKey = $"siteconfig:host:{normalised}";
        if (!_cache.TryGetValue(cacheKey, out SlugResponseDto? dto))
        {
            // Match the stored CustomDomain (which may or may not have "www.") against both
            // the raw and www-stripped versions of the incoming host.
            var site = await _context.Set<TenantSite>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.IsPublic &&
                    (s.CustomDomain == normalised ||
                     s.CustomDomain == "www." + normalised));

            if (site == null) return NotFound();

            dto = new SlugResponseDto(site.Slug);
            _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(10));
        }

        return Ok(dto);
    }
}

public record SlugResponseDto(string Slug);

public record SiteConfigDto(
    string  SiteName,
    string? LogoUrl,
    string? FaviconUrl,
    string  ThemeName,
    string  SkinPackage,
    string? SkinJson,
    string  DarkMode);
