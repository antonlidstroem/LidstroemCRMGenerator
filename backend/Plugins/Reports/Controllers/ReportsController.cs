using System.Globalization;
using System.Text;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Schema;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Reports.Controllers;

/// <summary>
/// Serves named reports registered by plugins via IReportProvider.
/// </summary>
[Route("api/reports")]
[ApiController]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IEnumerable<IReportProvider> _providers;
    private readonly DbContext _context;
    private readonly ITenantContext _tenantContext;

    public ReportsController(
        IEnumerable<IReportProvider> providers,
        DbContext context,
        ITenantContext tenantContext)
    {
        _providers = providers;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>Lists all available reports for this tenant (metadata only).</summary>
    [HttpGet]
    public ActionResult<IEnumerable<ReportMetadataDto>> GetReports() =>
        Ok(_providers
            .OrderBy(p => p.NavGroup)
            .ThenBy(p => p.NavOrder)
            .Select(p => new ReportMetadataDto(
                p.ReportKey,
                p.DisplayName,
                p.Description,
                p.NavGroup,
                p.NavOrder,
                p.Parameters)));

    /// <summary>Runs a named report. Returns JSON or CSV based on Accept header.</summary>
    [HttpPost("{key}/run")]
    public async Task<IActionResult> RunReport(
        string key,
        [FromBody] Dictionary<string, string?> parameters,
        [FromQuery] string? format = null)
    {
        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.ReportKey, key, StringComparison.OrdinalIgnoreCase));

        if (provider == null) return NotFound(new { error = $"Report '{key}' not found." });

        // Validate required parameters
        foreach (var param in provider.Parameters.Where(p => p.IsRequired))
        {
            if (!parameters.TryGetValue(param.Key, out var v) || string.IsNullOrWhiteSpace(v))
                return BadRequest(new { error = $"Parameter '{param.Key}' is required." });
        }

        var request = new ReportRunRequest
        {
            TenantId = _tenantContext.TenantId,
            Parameters = parameters,
            ExportFormat = format
        };

        var result = await provider.RunAsync(request, _context);

        // CSV export
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers.Accept.Any(a => a?.Contains("text/csv") == true))
        {
            return File(
                Encoding.UTF8.GetBytes(ToCsv(result)),
                "text/csv",
                $"{key}_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        return Ok(result);
    }

    // ── CSV serialiser ────────────────────────────────────────────────────────

    private static string ToCsv(ReportResult result)
    {
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine(string.Join(",", result.Columns.Select(c => CsvEscape(c.DisplayName))));

        // Data rows
        foreach (var row in result.Rows)
        {
            sb.AppendLine(string.Join(",", result.Columns.Select(c =>
            {
                row.TryGetValue(c.Key, out var v);
                return CsvEscape(FormatValue(v, c.Type));
            })));
        }

        // Summary row
        if (result.Summary != null)
        {
            sb.AppendLine(string.Join(",", result.Columns.Select(c =>
            {
                result.Summary.TryGetValue(c.Key, out var v);
                return CsvEscape(FormatValue(v, c.Type));
            })));
        }

        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        if (value == null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string FormatValue(object? value, ColumnType type)
    {
        if (value == null) return string.Empty;
        return type switch
        {
            ColumnType.Decimal  => Convert.ToDecimal(value).ToString("F2", CultureInfo.InvariantCulture),
            ColumnType.Currency => Convert.ToDecimal(value).ToString("F2", CultureInfo.InvariantCulture),
            ColumnType.Percent  => Convert.ToDecimal(value).ToString("P1", CultureInfo.InvariantCulture),
            ColumnType.Date     => value is DateTime d ? d.ToString("yyyy-MM-dd") : value.ToString()!,
            ColumnType.DateTime => value is DateTime dt ? dt.ToString("yyyy-MM-dd HH:mm") : value.ToString()!,
            _                   => value.ToString()!
        };
    }
}

/// <summary>
/// Ad-hoc aggregate queries against any registered entity type.
/// Validates all fields against the entity's schema before execution.
/// </summary>
[Route("api/query")]
[ApiController]
[Authorize]
public class QueryController : ControllerBase
{
    private readonly DbContext _context;
    private readonly SchemaRegistry _schemaRegistry;

    public QueryController(DbContext context, SchemaRegistry schemaRegistry)
    {
        _context = context;
        _schemaRegistry = schemaRegistry;
    }

    [HttpPost]
    public async Task<ActionResult<QueryResult>> RunQuery([FromBody] QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EntityType))
            return BadRequest(new { error = "entityType is required." });

        var schema = _schemaRegistry.Get(request.EntityType);
        if (schema == null)
            return NotFound(new { error = $"Entity type '{request.EntityType}' is not registered." });

        try
        {
            var engine = new QueryEngine(_context);
            var result = await engine.RunAsync(request, schema);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ReportMetadataDto(
    string ReportKey,
    string DisplayName,
    string? Description,
    string? NavGroup,
    int NavOrder,
    IReadOnlyList<ReportParameterDefinition> Parameters);
