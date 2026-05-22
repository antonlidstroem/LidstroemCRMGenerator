namespace Lidstroem.Frontend.Skins;

/// <summary>
/// All available skin packages. Each defines CSS custom property defaults.
/// Tenant-specific SkinJson overrides individual tokens at runtime.
/// </summary>
public static class SkinPackages
{
    public static readonly IReadOnlyDictionary<string, SkinTokens> All =
        new Dictionary<string, SkinTokens>
        {
            ["Slate"] = new SkinTokens
            {
                ColorPrimary        = "#2563EB",
                ColorPrimaryDark    = "#1D4ED8",
                ColorSecondary      = "#64748B",
                ColorSurface        = "#F8FAFC",
                ColorBackground     = "#FFFFFF",
                ColorText           = "#0F172A",
                ColorTextMuted      = "#64748B",
                ColorBorder         = "#E2E8F0",
                ColorDanger         = "#DC2626",
                ColorSuccess        = "#16A34A",
                // Dark mode variants
                ColorSurfaceDark    = "#1E293B",
                ColorBackgroundDark = "#0F172A",
                ColorTextDark       = "#F1F5F9",
                ColorTextMutedDark  = "#94A3B8",
                ColorBorderDark     = "#334155",
                // Typography
                FontHeading         = "DM Sans",
                FontBody            = "DM Sans",
                FontSizeBase        = "16px",
                FontWeightHeading   = "700",
                LineHeightBase      = "1.6",
                // Shape
                RadiusSm            = "6px",
                RadiusMd            = "10px",
                RadiusLg            = "16px",
                RadiusFull          = "9999px",
                ShadowCard          = "0 1px 3px rgba(0,0,0,.08), 0 1px 2px rgba(0,0,0,.06)",
                ShadowDropdown      = "0 10px 15px -3px rgba(0,0,0,.1)",
                SidebarWidth        = "256px",
                ContentMaxWidth     = "1280px",
            },

            ["Forest"] = new SkinTokens
            {
                ColorPrimary        = "#2D6A4F",
                ColorPrimaryDark    = "#1B4332",
                ColorSecondary      = "#95D5B2",
                ColorSurface        = "#F0F7F4",
                ColorBackground     = "#FAFDF9",
                ColorText           = "#1B2E24",
                ColorTextMuted      = "#52796F",
                ColorBorder         = "#D8EAE1",
                ColorDanger         = "#C0392B",
                ColorSuccess        = "#27AE60",
                ColorSurfaceDark    = "#1B2E24",
                ColorBackgroundDark = "#0D1F18",
                ColorTextDark       = "#D8EAE1",
                ColorTextMutedDark  = "#95D5B2",
                ColorBorderDark     = "#2D6A4F",
                FontHeading         = "Fraunces",
                FontBody            = "Source Serif 4",
                FontSizeBase        = "16px",
                FontWeightHeading   = "700",
                LineHeightBase      = "1.7",
                RadiusSm            = "4px",
                RadiusMd            = "8px",
                RadiusLg            = "12px",
                RadiusFull          = "9999px",
                ShadowCard          = "0 2px 8px rgba(29,67,51,.1)",
                ShadowDropdown      = "0 8px 24px rgba(29,67,51,.15)",
                SidebarWidth        = "260px",
                ContentMaxWidth     = "1200px",
            },

            ["Ember"] = new SkinTokens
            {
                ColorPrimary        = "#EA580C",
                ColorPrimaryDark    = "#C2410C",
                ColorSecondary      = "#FED7AA",
                ColorSurface        = "#1C1917",
                ColorBackground     = "#0C0A09",
                ColorText           = "#FAFAF9",
                ColorTextMuted      = "#A8A29E",
                ColorBorder         = "#292524",
                ColorDanger         = "#EF4444",
                ColorSuccess        = "#22C55E",
                ColorSurfaceDark    = "#1C1917",
                ColorBackgroundDark = "#0C0A09",
                ColorTextDark       = "#FAFAF9",
                ColorTextMutedDark  = "#A8A29E",
                ColorBorderDark     = "#292524",
                FontHeading         = "Syne",
                FontBody            = "Inter",
                FontSizeBase        = "15px",
                FontWeightHeading   = "800",
                LineHeightBase      = "1.5",
                RadiusSm            = "2px",
                RadiusMd            = "4px",
                RadiusLg            = "8px",
                RadiusFull          = "9999px",
                ShadowCard          = "0 0 0 1px rgba(234,88,12,.2), 0 4px 16px rgba(0,0,0,.4)",
                ShadowDropdown      = "0 12px 32px rgba(0,0,0,.5)",
                SidebarWidth        = "240px",
                ContentMaxWidth     = "1400px",
            },

            ["Lavender"] = new SkinTokens
            {
                ColorPrimary        = "#7C3AED",
                ColorPrimaryDark    = "#6D28D9",
                ColorSecondary      = "#C4B5FD",
                ColorSurface        = "#F5F3FF",
                ColorBackground     = "#FDFCFF",
                ColorText           = "#1E1B4B",
                ColorTextMuted      = "#7C3AED",
                ColorBorder         = "#DDD6FE",
                ColorDanger         = "#DC2626",
                ColorSuccess        = "#059669",
                ColorSurfaceDark    = "#1E1B4B",
                ColorBackgroundDark = "#13111F",
                ColorTextDark       = "#EDE9FE",
                ColorTextMutedDark  = "#A78BFA",
                ColorBorderDark     = "#4C1D95",
                FontHeading         = "Playfair Display",
                FontBody            = "Lato",
                FontSizeBase        = "16px",
                FontWeightHeading   = "700",
                LineHeightBase      = "1.65",
                RadiusSm            = "8px",
                RadiusMd            = "12px",
                RadiusLg            = "20px",
                RadiusFull          = "9999px",
                ShadowCard          = "0 4px 12px rgba(124,58,237,.1)",
                ShadowDropdown      = "0 12px 28px rgba(124,58,237,.15)",
                SidebarWidth        = "264px",
                ContentMaxWidth     = "1200px",
            },

            ["Minimal"] = new SkinTokens
            {
                ColorPrimary        = "#111827",
                ColorPrimaryDark    = "#000000",
                ColorSecondary      = "#6B7280",
                ColorSurface        = "#F9FAFB",
                ColorBackground     = "#FFFFFF",
                ColorText           = "#111827",
                ColorTextMuted      = "#6B7280",
                ColorBorder         = "#E5E7EB",
                ColorDanger         = "#EF4444",
                ColorSuccess        = "#10B981",
                ColorSurfaceDark    = "#111827",
                ColorBackgroundDark = "#030712",
                ColorTextDark       = "#F9FAFB",
                ColorTextMutedDark  = "#9CA3AF",
                ColorBorderDark     = "#1F2937",
                FontHeading         = "Montserrat",
                FontBody            = "Open Sans",
                FontSizeBase        = "15px",
                FontWeightHeading   = "600",
                LineHeightBase      = "1.6",
                RadiusSm            = "2px",
                RadiusMd            = "4px",
                RadiusLg            = "6px",
                RadiusFull          = "9999px",
                ShadowCard          = "0 1px 2px rgba(0,0,0,.05)",
                ShadowDropdown      = "0 4px 6px rgba(0,0,0,.07)",
                SidebarWidth        = "220px",
                ContentMaxWidth     = "1100px",
            },
        };

    public static SkinTokens GetPackage(string name) =>
        All.TryGetValue(name, out var tokens) ? tokens : All["Slate"];
}

public class SkinTokens
{
    // Colors
    public string ColorPrimary        { get; init; } = "#2563EB";
    public string ColorPrimaryDark    { get; init; } = "#1D4ED8";
    public string ColorSecondary      { get; init; } = "#64748B";
    public string ColorSurface        { get; init; } = "#F8FAFC";
    public string ColorBackground     { get; init; } = "#FFFFFF";
    public string ColorText           { get; init; } = "#0F172A";
    public string ColorTextMuted      { get; init; } = "#64748B";
    public string ColorBorder         { get; init; } = "#E2E8F0";
    public string ColorDanger         { get; init; } = "#DC2626";
    public string ColorSuccess        { get; init; } = "#16A34A";

    // Dark mode color overrides
    public string ColorSurfaceDark    { get; init; } = "#1E293B";
    public string ColorBackgroundDark { get; init; } = "#0F172A";
    public string ColorTextDark       { get; init; } = "#F1F5F9";
    public string ColorTextMutedDark  { get; init; } = "#94A3B8";
    public string ColorBorderDark     { get; init; } = "#334155";

    // Typography
    public string FontHeading         { get; init; } = "DM Sans";
    public string FontBody            { get; init; } = "DM Sans";
    public string FontSizeBase        { get; init; } = "16px";
    public string FontWeightHeading   { get; init; } = "700";
    public string LineHeightBase      { get; init; } = "1.6";

    // Shape
    public string RadiusSm            { get; init; } = "6px";
    public string RadiusMd            { get; init; } = "10px";
    public string RadiusLg            { get; init; } = "16px";
    public string RadiusFull          { get; init; } = "9999px";
    public string ShadowCard          { get; init; } = "0 1px 3px rgba(0,0,0,.08)";
    public string ShadowDropdown      { get; init; } = "0 10px 15px -3px rgba(0,0,0,.1)";

    // Layout
    public string SidebarWidth        { get; init; } = "256px";
    public string ContentMaxWidth     { get; init; } = "1280px";
}
