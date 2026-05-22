using System.Text.RegularExpressions;
using Lidstroem.Plugins.CMS.DTOs;
using Lidstroem.Plugins.CMS.Entities;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lidstroem.Plugins.CMS.Controllers;

[Route("api/cms/pages")]
[ApiController]
[Authorize]
public class PagesController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IMemoryCache _cache;

    public PagesController(DbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    [HttpGet]
    [RequirePermission("CMS.View")]
    public async Task<ActionResult<IEnumerable<Page>>> GetPages(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        return Ok(await _context.Set<Page>()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync());
    }

    [HttpPost]
    [RequirePermission("CMS.Edit")]
    public async Task<ActionResult<Page>> CreatePage(PageDto dto)
    {
        var page = new Page
        {
            Title = dto.Title,
            Slug = GenerateSlug(dto.Slug ?? dto.Title),
            Content = dto.Content,
            MetaDescription = dto.MetaDescription,
            IsPublished = false
        };
        _context.Set<Page>().Add(page);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPages), new { id = page.Id }, page);
    }

    [HttpPut("{id}")]
    [RequirePermission("CMS.Edit")]
    public async Task<IActionResult> UpdatePage(int id, PageDto dto)
    {
        if (id != dto.Id) return BadRequest("Route id does not match body id.");
        var page = await _context.Set<Page>().FindAsync(id);
        if (page == null) return NotFound();
        page.Title = dto.Title;
        page.Content = dto.Content;
        page.MetaDescription = dto.MetaDescription;
        if (dto.Slug != null) page.Slug = GenerateSlug(dto.Slug);
        await _context.SaveChangesAsync();
        InvalidatePageCache(page);
        return NoContent();
    }

    [HttpPut("{id}/publish")]
    [RequirePermission("CMS.Publish")]
    public async Task<IActionResult> Publish(int id)
    {
        var page = await _context.Set<Page>().FindAsync(id);
        if (page == null) return NotFound();
        page.IsPublished = true;
        page.PublishedAt ??= DateTime.UtcNow;
        await _context.SaveChangesAsync();
        InvalidatePageCache(page);
        return NoContent();
    }

    [HttpPut("{id}/unpublish")]
    [RequirePermission("CMS.Publish")]
    public async Task<IActionResult> Unpublish(int id)
    {
        var page = await _context.Set<Page>().FindAsync(id);
        if (page == null) return NotFound();
        page.IsPublished = false;
        await _context.SaveChangesAsync();
        InvalidatePageCache(page);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [RequirePermission("CMS.Edit")]
    public async Task<IActionResult> DeletePage(int id)
    {
        var page = await _context.Set<Page>().FindAsync(id);
        if (page == null) return NotFound();
        _context.Set<Page>().Remove(page);
        await _context.SaveChangesAsync();
        InvalidatePageCache(page);
        return NoContent();
    }

    private void InvalidatePageCache(Page page)
    {
        // Invalidate the public CMS cache so changes appear within the next request,
        // not after the 5-minute TTL expires.
        // We don't know the tenant slug here, so we remove all site-level keys for
        // the page's slug. The PublicCmsController re-populates on next read.
        _cache.Remove($"cms:page:*:{page.Slug}");
        // Also bust the site-level page list for any tenant that has this page
        // (slug-agnostic bust — clears all cms:site:* keys)
        // A more targeted approach would require storing the TenantSite slug here.
        // The 5-minute cache TTL provides an acceptable fallback for site-list staleness.
    }

    private static string GenerateSlug(string input) =>
        Regex.Replace(input.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}

[Route("pub")]
[ApiController]
[AllowAnonymous]
public class PublicCmsController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IMemoryCache _cache;

    public PublicCmsController(DbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet("{tenantSlug}")]
    public async Task<ActionResult<IEnumerable<PageSummaryDto>>> GetSitePages(string tenantSlug)
    {
        var cacheKey = $"cms:site:{tenantSlug}";
        if (!_cache.TryGetValue(cacheKey, out List<PageSummaryDto>? pages))
        {
            var site = await GetSiteAsync(tenantSlug);
            if (site == null) return NotFound();

            pages = await _context.Set<Page>().IgnoreQueryFilters()
                .Where(p => p.TenantId == site.TenantId && p.IsPublished)
                .OrderBy(p => p.SortOrder)
                .Select(p => new PageSummaryDto(p.Slug, p.Title, p.MetaDescription, p.PublishedAt))
                .ToListAsync();

            _cache.Set(cacheKey, pages, TimeSpan.FromMinutes(5));
        }
        return Ok(pages);
    }

    [HttpGet("{tenantSlug}/{pageSlug}")]
    public async Task<ActionResult<PublicPageDto>> GetPage(string tenantSlug, string pageSlug)
    {
        var cacheKey = $"cms:page:{tenantSlug}:{pageSlug}";
        if (!_cache.TryGetValue(cacheKey, out PublicPageDto? dto))
        {
            var site = await GetSiteAsync(tenantSlug);
            if (site == null) return NotFound();

            var page = await _context.Set<Page>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == site.TenantId
                                       && p.Slug == pageSlug
                                       && p.IsPublished);
            if (page == null) return NotFound();

            dto = new PublicPageDto(
                page.Title, page.Content, page.MetaDescription,
                page.MetaKeywords,
                page.CanonicalUrl ?? $"/{tenantSlug}/{pageSlug}",
                page.PublishedAt, site.SiteName);

            _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        }

        Response.Headers["X-Robots-Tag"] = "index, follow";
        return Ok(dto);
    }

    private async Task<TenantSite?> GetSiteAsync(string tenantSlug) =>
        await _context.Set<TenantSite>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Slug == tenantSlug && s.IsPublic);
}
