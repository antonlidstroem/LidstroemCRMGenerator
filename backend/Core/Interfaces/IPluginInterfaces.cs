using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Core.Interfaces;

public interface IPluginMetadata
{
    string PluginKey { get; }
    string RoutePrefix { get; }
}

public interface IPluginModelConfigurator
{
    void ConfigureModel(ModelBuilder modelBuilder);
}

public interface IEntityExtensionProvider
{
    string TargetEntityName { get; }
    Task<object?> GetExtensionDataAsync(int entityId, DbContext context);
}

public interface ILinkResolver
{
    string TargetType { get; }
    Task<string?> ResolveNameAsync(int targetId, DbContext context);
}

public interface ILinkResolverService
{
    Task<string?> ResolveAsync(int targetId, string targetType);
    IReadOnlyCollection<string> RegisteredTypes { get; }
}

public interface IPolymorphicTarget
{
    int TargetId { get; set; }
    string TargetType { get; set; }
}

public interface ICustomPageMetadata
{
    string PageKey { get; }
    string DisplayName { get; }
    string? Description { get; }
    string Route { get; }
    string? NavGroup { get; }
    int NavOrder { get; }
    string? Icon { get; }
}

public interface IRealtimeNotifier
{
    Task NotifyEntityChangedAsync(
        Guid tenantId,
        string entityType,
        int entityId,
        ChangeType changeType,
        CancellationToken cancellationToken = default);

    Task NotifyCustomAsync(
        Guid tenantId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}

public enum ChangeType { Created = 1, Updated = 2, Deleted = 3 }

public interface IRealtimeBroadcast
{
    Guid TenantId { get; }
    string EntityType { get; }
    int EntityId { get; }
    ChangeType ChangeType { get; }
}

// ── Reports ───────────────────────────────────────────────────────────────────

/// <summary>
/// Implemented by plugins to expose named reports.
/// Discovered at startup via assembly scanning — no manual registration needed.
///
/// Example:
///   public class DonationsByProjectReport : IReportProvider
///   {
///       public string ReportKey    => "DonationsByProject";
///       public string DisplayName  => "Donations by project";
///       public string? NavGroup    => "Donations";
///       public int NavOrder        => 10;
///       public string? Description => "Total confirmed donations grouped by project.";
///       public IReadOnlyList<ReportParameterDefinition> Parameters => new[]
///       {
///           new ReportParameterDefinition("dateFrom", "From",   ParameterType.Date,  false),
///           new ReportParameterDefinition("dateTo",   "To",     ParameterType.Date,  false),
///           new ReportParameterDefinition("status",   "Status", ParameterType.Enum,  false,
///               EnumOptions: new[]{"All","Confirmed","Pending"}.ToList())
///       };
///
///       public async Task<ReportResult> RunAsync(
///           ReportRunRequest request, DbContext context) { ... }
///   }
/// </summary>
public interface IReportProvider
{
    string ReportKey { get; }
    string DisplayName { get; }
    string? Description { get; }
    string? NavGroup { get; }
    int NavOrder { get; }
    IReadOnlyList<ReportParameterDefinition> Parameters { get; }
    Task<ReportResult> RunAsync(ReportRunRequest request, DbContext context);
}

public record ReportParameterDefinition(
    string Key,
    string DisplayName,
    ParameterType Type,
    bool IsRequired,
    string? Placeholder = null,
    IReadOnlyList<string>? EnumOptions = null);

public enum ParameterType { Text, Number, Date, DateRange, Enum, Boolean }

public class ReportRunRequest
{
    public Guid TenantId { get; init; }
    public Dictionary<string, string?> Parameters { get; init; } = new();
    public string? ExportFormat { get; init; } // null = JSON, "csv" = CSV download

    public string? GetString(string key) =>
        Parameters.TryGetValue(key, out var v) ? v : null;

    public DateTime? GetDate(string key) =>
        DateTime.TryParse(GetString(key), out var d) ? d : null;

    public int? GetInt(string key) =>
        int.TryParse(GetString(key), out var i) ? i : null;

    public decimal? GetDecimal(string key) =>
        decimal.TryParse(GetString(key), out var d) ? d : null;
}

public class ReportResult
{
    /// <summary>Column definitions for table display and CSV headers.</summary>
    public IReadOnlyList<ReportColumn> Columns { get; init; } = new List<ReportColumn>();

    /// <summary>Each row is a dictionary of columnKey → display value.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
        = new List<IReadOnlyDictionary<string, object?>>();

    /// <summary>Optional summary row shown at the bottom of the table.</summary>
    public IReadOnlyDictionary<string, object?>? Summary { get; init; }

    /// <summary>Optional chart hint — if set, the frontend renders a chart alongside the table.</summary>
    public ReportChartHint? ChartHint { get; init; }

    /// <summary>Total row count before any pagination (for display only).</summary>
    public int TotalRows => Rows.Count;
}

public record ReportColumn(
    string Key,
    string DisplayName,
    ColumnType Type = ColumnType.Text,
    bool IsSortable = false,
    string? Format = null); // e.g. "N0", "P1", "yyyy-MM-dd"

public enum ColumnType { Text, Number, Decimal, Date, DateTime, Percent, Currency }

public record ReportChartHint(
    string LabelColumn,
    string ValueColumn,
    string ChartType = "bar"); // "bar" | "line" | "pie"
