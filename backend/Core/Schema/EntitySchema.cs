namespace Lidstroem.Core.Schema;

public class EntitySchema
{
    public string EntityType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DisplayNamePlural { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string ApiBasePath { get; init; } = string.Empty;

    /// <summary>
    /// FIX #15: Explicit plugin ownership — replaces the RoutePrefix substring heuristic
    /// in PluginManifestService that could match unrelated schemas.
    /// Must match IPluginMetadata.PluginKey exactly (e.g. "CMS", "WorkManagement.Projects").
    /// </summary>
    public string OwnerPluginKey { get; init; } = string.Empty;

    /// <summary>
    /// Groups this entity under a nav section in the sidebar.
    /// Null = top-level. Examples: "Work Management", "Communication", "Admin"
    /// </summary>
    public string? NavGroup { get; init; }

    /// <summary>
    /// Controls sort order within the nav. Lower = higher up.
    /// </summary>
    public int NavOrder { get; init; } = 100;

    /// <summary>
    /// When true the entity appears as a count-card on the dashboard.
    /// Defaults to true so all entities show up unless explicitly excluded.
    /// </summary>
    public bool ShowOnDashboard { get; init; } = true;

    public IReadOnlyList<FieldDefinition> Fields { get; init; } = new List<FieldDefinition>();
    public IReadOnlyList<ActionDefinition> Actions { get; init; } = new List<ActionDefinition>();
    public IReadOnlyList<string> DefaultListColumns { get; init; } = new List<string>();
}

public class FieldDefinition
{
    public string FieldName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public FieldType Type { get; init; }
    public string? RelatedEntityType { get; init; }
    public IReadOnlyList<EnumOption>? EnumOptions { get; init; }
    public bool IsRequired { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public bool ShowInList { get; init; } = true;
    public bool ShowInForm { get; init; } = true;
    public bool IsReadOnly { get; init; }
    public bool IsSortable { get; init; } = true;
    public bool IsFilterable { get; init; }
    public int SortOrder { get; init; }
    public string? UiHint { get; init; }
}

public enum FieldType
{
    Text = 1, Number = 2, Decimal = 3, Boolean = 4,
    Date = 5, DateTime = 6, Enum = 7, Relation = 8,
    MultiRelation = 9, RichText = 10, File = 11
}

public record EnumOption(string Value, string DisplayName);

public class ActionDefinition
{
    public string ActionKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string HttpMethod { get; init; } = "POST";
    public string UrlTemplate { get; init; } = string.Empty;
    public string? RequiredPermission { get; init; }
    public ActionPlacement Placement { get; init; } = ActionPlacement.Row;
}

public enum ActionPlacement { Row = 1, Toolbar = 2, Form = 3 }

public interface ISchemaProvider
{
    IReadOnlyCollection<EntitySchema> GetSchemas();
}
