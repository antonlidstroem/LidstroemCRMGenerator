using System.Text.Json;
using Microsoft.JSInterop;

namespace Lidstroem.Frontend.Core.Services;

/// <summary>
/// Fetches report metadata and runs named reports or ad-hoc queries.
/// Injected into ReportPage.razor and ReportWidget.razor.
/// </summary>
public class ReportService
{
    private readonly ApiClient _api;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    // Cached metadata — loaded once per session
    private List<ReportMetadataVm>? _cache;

    public ReportService(ApiClient api) => _api = api;

    public async Task<List<ReportMetadataVm>> GetAvailableAsync()
    {
        if (_cache != null) return _cache;
        var items = await _api.GetListAsync("/api/reports");
        _cache = items?.Select(MapMetadata).ToList() ?? new();
        return _cache;
    }

    public async Task<ReportMetadataVm?> GetAsync(string key)
    {
        var all = await GetAvailableAsync();
        return all.FirstOrDefault(r =>
            string.Equals(r.ReportKey, key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ReportResultVm?> RunAsync(
        string key, Dictionary<string, string?> parameters)
    {
        var json = await _api.PostAsync(
            $"/api/reports/{Uri.EscapeDataString(key)}/run", parameters);
        if (json == null) return null;
        return JsonSerializer.Deserialize<ReportResultVm>(json.Value.GetRawText(), _jsonOptions);
    }

    public async Task<ReportResultVm?> QueryAsync(QueryRequestVm request)
    {
        var json = await _api.PostAsync("/api/query", request);
        if (json == null) return null;
        return JsonSerializer.Deserialize<ReportResultVm>(json.Value.GetRawText(), _jsonOptions);
    }

    // BUG-29 FIX: GetCsvDownloadUrl returned a bare href for use in <a href="...">.
    // Browser-initiated requests carry no Authorization header — the endpoint
    // returns 401 for every authenticated user. Fix: fetch via ApiClient (which
    // injects Bearer token), convert to a JS blob URL and trigger download in browser.
    public async Task DownloadCsvAsync(
        IJSRuntime js, string key, Dictionary<string, string?> parameters)
    {
        var qs = string.Join("&", parameters
            .Where(p => p.Value != null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
        var url = $"/api/reports/{Uri.EscapeDataString(key)}/run?format=csv&{qs}";

        var bytes = await _api.GetBytesAsync(url);
        if (bytes == null) return;

        // Trigger a client-side download via a JS Blob URL — no page navigation needed.
        await js.InvokeVoidAsync("lidstroem.downloadBytes", bytes, key + ".csv", "text/csv");
    }

    [Obsolete("Use DownloadCsvAsync. Direct href has no auth header → 401. (BUG-29)")]
    public string GetCsvDownloadUrl(string key, Dictionary<string, string?> parameters)
    {
        var qs = string.Join("&", parameters
            .Where(p => p.Value != null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
        return $"/api/reports/{Uri.EscapeDataString(key)}/run?format=csv&{qs}";
    }

    public void InvalidateCache() => _cache = null;

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static ReportMetadataVm MapMetadata(JsonElement el)
    {
        var parameters = new List<ReportParameterVm>();
        if (el.TryGetProperty("parameters", out var paramArr))
        {
            foreach (var p in paramArr.EnumerateArray())
            {
                var enumOptions = new List<string>();
                if (p.TryGetProperty("enumOptions", out var opts) &&
                    opts.ValueKind == JsonValueKind.Array)
                    enumOptions = opts.EnumerateArray()
                        .Select(o => o.GetString() ?? "")
                        .ToList();

                parameters.Add(new ReportParameterVm(
                    Key: Str(p, "key"),
                    DisplayName: Str(p, "displayName"),
                    Type: Str(p, "type"),
                    IsRequired: p.TryGetProperty("isRequired", out var r) && r.GetBoolean(),
                    Placeholder: NullStr(p, "placeholder"),
                    EnumOptions: enumOptions));
            }
        }

        return new ReportMetadataVm(
            ReportKey: Str(el, "reportKey"),
            DisplayName: Str(el, "displayName"),
            Description: NullStr(el, "description"),
            NavGroup: NullStr(el, "navGroup"),
            NavOrder: el.TryGetProperty("navOrder", out var o) ? o.GetInt32() : 100,
            Parameters: parameters);
    }

    private static string Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) ? p.GetString() ?? string.Empty : string.Empty;

    private static string? NullStr(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) ? p.GetString() : null;
}

// ── View models ───────────────────────────────────────────────────────────────

public record ReportMetadataVm(
    string ReportKey,
    string DisplayName,
    string? Description,
    string? NavGroup,
    int NavOrder,
    List<ReportParameterVm> Parameters);

public record ReportParameterVm(
    string Key,
    string DisplayName,
    string Type,       // "Text" | "Number" | "Date" | "DateRange" | "Enum" | "Boolean"
    bool IsRequired,
    string? Placeholder,
    List<string> EnumOptions);

public class ReportResultVm
{
    public List<ReportColumnVm> Columns { get; init; } = new();
    public List<Dictionary<string, JsonElement>> Rows { get; init; } = new();
    public Dictionary<string, JsonElement>? Summary { get; init; }
    public ReportChartHintVm? ChartHint { get; init; }
    public int TotalRows => Rows.Count;
}

public record ReportColumnVm(
    string Key,
    string DisplayName,
    string Type,       // "Text" | "Number" | "Decimal" | "Date" | "DateTime" | "Currency" | "Percent"
    bool IsSortable,
    string? Format);

public record ReportChartHintVm(
    string LabelColumn,
    string ValueColumn,
    string ChartType);  // "bar" | "line" | "pie"

public class QueryRequestVm
{
    public string EntityType { get; init; } = string.Empty;
    public List<string>? Fields { get; init; }
    public string? GroupBy { get; init; }
    public List<AggregateSpecVm>? Aggregate { get; init; }
    public List<QueryFilterVm>? Filters { get; init; }
    public DateRangeFilterVm? DateRange { get; init; }
    public int? Limit { get; init; }
}

public record AggregateSpecVm(string Field, string Fn); // Fn: "Count"|"Sum"|"Avg"|"Min"|"Max"
public record QueryFilterVm(string Field, string Op, string? Value);
public record DateRangeFilterVm(string Field, DateTime? From, DateTime? To);
