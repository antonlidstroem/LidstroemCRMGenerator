using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Lidstroem.Plugins.Schema.Controllers;

[Route("api/plugin-manifest")]
[ApiController]
[Authorize]
public class PluginManifestController : ControllerBase
{
    private readonly PluginManifestService _manifestService;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "plugin:manifest";

    public PluginManifestController(PluginManifestService manifestService, IMemoryCache cache)
    {
        _manifestService = manifestService;
        _cache = cache;
    }

    /// <summary>
    /// Returns the full system manifest: registered plugins, core contracts,
    /// active entity types, permissions, and invariant plugin rules.
    ///
    /// Intended for use by AI agents building new plugins — feed this response
    /// as context alongside the plugin template.
    /// </summary>
    [HttpGet]
    public ActionResult<PluginManifest> GetManifest([FromQuery] bool refresh = false)
    {
        if (refresh) _cache.Remove(CacheKey);

        var manifest = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return _manifestService.Build();
        });

        return Ok(manifest);
    }
}
