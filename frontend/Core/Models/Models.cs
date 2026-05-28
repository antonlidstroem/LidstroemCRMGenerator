namespace Lidstroem.Frontend.Core.Models;

// ── Schema ────────────────────────────────────────────────────────────────────

public record EntitySchema(
    string EntityType,
    string DisplayName,
    string DisplayNamePlural,
    string? Icon,
    string ApiBasePath,
    string? NavGroup,
    int NavOrder,
    bool ShowOnDashboard,
    IReadOnlyList<FieldDefinition> Fields,
    IReadOnlyList<ActionDefinition> Actions,
    IReadOnlyList<string> DefaultListColumns);

public record FieldDefinition(
    string FieldName,
    string DisplayName,
    string? Description,
    FieldType Type,
    string? RelatedEntityType,
    IReadOnlyList<EnumOption>? EnumOptions,
    bool IsRequired,
    int? MinLength,
    int? MaxLength,
    decimal? Min,
    decimal? Max,
    bool ShowInList,
    bool ShowInForm,
    bool IsReadOnly,
    bool IsSortable,
    bool IsFilterable,
    int SortOrder,
    string? UiHint);

public enum FieldType
{
    Text = 1, Number = 2, Decimal = 3, Boolean = 4,
    Date = 5, DateTime = 6, Enum = 7, Relation = 8,
    MultiRelation = 9, RichText = 10, File = 11
}

public record EnumOption(string Value, string DisplayName);

public record ActionDefinition(
    string ActionKey,
    string DisplayName,
    string? Icon,
    string HttpMethod,
    string UrlTemplate,
    string? RequiredPermission,
    ActionPlacement Placement);

public enum ActionPlacement { Row = 1, Toolbar = 2, Form = 3 }

// ── Site config (theme/skin) ──────────────────────────────────────────────────

public record SiteConfig(
    string SiteName,
    string? LogoUrl,
    string? FaviconUrl,
    string ThemeName,
    string SkinPackage,
    string? SkinJson,
    string DarkMode);

// ── Auth ──────────────────────────────────────────────────────────────────────

public record LoginRequest(string Identifier, string Password);
public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

// ── Pagination ────────────────────────────────────────────────────────────────

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// ── Nav helpers ───────────────────────────────────────────────────────────────

public record NavGroup(string? Name, IReadOnlyList<EntitySchema> Schemas);
