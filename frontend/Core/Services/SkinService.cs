using System.Text.Json;
using System.Text.RegularExpressions;
using Lidstroem.Frontend.Core.Models;
using Lidstroem.Frontend.Skins;
using Microsoft.JSInterop;

namespace Lidstroem.Frontend.Core.Services;

/// <summary>
/// Applies skin tokens to the document root as CSS custom properties.
/// Called before first render to prevent FOUC.
/// </summary>
public class SkinService
{
    private readonly IJSRuntime _js;

    // FIX #7: Allowlists for tenant-controlled SkinJson overrides.
    // Keys must be valid CSS custom property names (--prefix-name).
    // Values are validated against type-specific patterns before DOM injection.
    private static readonly Regex ValidKeyPattern =
        new(@"^--[a-z][a-z0-9-]*$", RegexOptions.Compiled);

    private static readonly Regex HexColorPattern =
        new(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

    private static readonly Regex CssLengthPattern =
        new(@"^\d+(\.\d+)?(px|rem|em|%|vw|vh)$", RegexOptions.Compiled);

    private static readonly Regex CssNumberPattern =
        new(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

    // Safe font names: letters, numbers, spaces, hyphens, underscores, commas, quotes
    private static readonly Regex FontNamePattern =
        new(@"^[A-Za-z0-9 ,_'\-]+$", RegexOptions.Compiled);

    // Safe shadow: only digits, units, rgba/rgb, commas, spaces, dots
    private static readonly Regex ShadowPattern =
        new(@"^[0-9a-z,.()\s#%rgba]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SkinService(IJSRuntime js) => _js = js;

    public async Task ApplyAsync(SiteConfig config, bool prefersDark)
    {
        var tokens = BuildTokenDictionary(config, prefersDark);
        await _js.InvokeVoidAsync("lidstroem.applySkin", tokens, config.ThemeName, prefersDark);
    }

    private static Dictionary<string, string> BuildTokenDictionary(SiteConfig config, bool prefersDark)
    {
        var package = SkinPackages.GetPackage(config.SkinPackage);
        var tokens = new Dictionary<string, string>
        {
            ["--color-primary"]         = package.ColorPrimary,
            ["--color-primary-dark"]    = package.ColorPrimaryDark,
            ["--color-secondary"]       = package.ColorSecondary,
            ["--color-surface"]         = prefersDark ? package.ColorSurfaceDark    : package.ColorSurface,
            ["--color-background"]      = prefersDark ? package.ColorBackgroundDark : package.ColorBackground,
            ["--color-text"]            = prefersDark ? package.ColorTextDark       : package.ColorText,
            ["--color-text-muted"]      = prefersDark ? package.ColorTextMutedDark  : package.ColorTextMuted,
            ["--color-border"]          = prefersDark ? package.ColorBorderDark     : package.ColorBorder,
            ["--color-danger"]          = package.ColorDanger,
            ["--color-success"]         = package.ColorSuccess,
            ["--font-heading"]          = package.FontHeading,
            ["--font-body"]             = package.FontBody,
            ["--font-size-base"]        = package.FontSizeBase,
            ["--font-weight-heading"]   = package.FontWeightHeading,
            ["--line-height-base"]      = package.LineHeightBase,
            ["--radius-sm"]             = package.RadiusSm,
            ["--radius-md"]             = package.RadiusMd,
            ["--radius-lg"]             = package.RadiusLg,
            ["--radius-full"]           = package.RadiusFull,
            ["--shadow-card"]           = package.ShadowCard,
            ["--shadow-dropdown"]       = package.ShadowDropdown,
            ["--sidebar-width"]         = package.SidebarWidth,
            ["--content-max-width"]     = package.ContentMaxWidth,
        };

        // FIX #7: Tenant-controlled overrides are sanitised before merging.
        // Invalid keys or values are silently discarded (logged server-side separately).
        if (!string.IsNullOrWhiteSpace(config.SkinJson))
        {
            try
            {
                var overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(config.SkinJson);
                if (overrides != null)
                {
                    foreach (var (key, value) in overrides)
                    {
                        if (!IsSafeKey(key) || !IsSafeValue(key, value))
                            continue; // skip invalid — never inject untrusted CSS

                        tokens[key] = value;
                    }
                }
            }
            catch { /* malformed SkinJson — use package defaults */ }
        }

        return tokens;
    }

    /// <summary>Key must be a valid CSS custom property: --lowercase-name</summary>
    private static bool IsSafeKey(string key) =>
        !string.IsNullOrEmpty(key) && ValidKeyPattern.IsMatch(key);

    /// <summary>Value must match a pattern appropriate for the token category.</summary>
    private static bool IsSafeValue(string key, string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 200) return false;

        if (key.StartsWith("--color-"))       return HexColorPattern.IsMatch(value);
        if (key.StartsWith("--font-size-"))   return CssLengthPattern.IsMatch(value);
        if (key.StartsWith("--font-weight-")) return CssNumberPattern.IsMatch(value);
        if (key.StartsWith("--line-height-")) return CssNumberPattern.IsMatch(value);
        if (key.StartsWith("--radius-"))      return CssLengthPattern.IsMatch(value);
        if (key.StartsWith("--sidebar-"))     return CssLengthPattern.IsMatch(value);
        if (key.StartsWith("--content-"))     return CssLengthPattern.IsMatch(value);
        if (key.StartsWith("--shadow-"))      return ShadowPattern.IsMatch(value);
        if (key.StartsWith("--font-"))        return FontNamePattern.IsMatch(value);

        // Unknown token categories are rejected by default
        return false;
    }
}
