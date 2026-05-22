// Example: how a plugin implements IReportProvider
// Place this file in Plugins/WorkManagement.Projects/ (or any plugin)
// It is discovered automatically at startup — no manual registration.

using Lidstroem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.WorkManagement.Projects.Reports;

public class ProjectActivitySummaryReport : IReportProvider
{
    public string ReportKey   => "ProjectActivitySummary";
    public string DisplayName => "Activity summary by project";
    public string? Description => "Count and last-activity date per project, filterable by date range.";
    public string? NavGroup   => "Work Management";
    public int NavOrder        => 20;

    public IReadOnlyList<ReportParameterDefinition> Parameters => new[]
    {
        new ReportParameterDefinition("dateFrom", "From date", ParameterType.Date,  false),
        new ReportParameterDefinition("dateTo",   "To date",   ParameterType.Date,  false),
        new ReportParameterDefinition("status",   "Status",    ParameterType.Enum,  false,
            EnumOptions: new List<string> { "All", "Open", "Closed" })
    };

    public async Task<ReportResult> RunAsync(ReportRunRequest request, DbContext context)
    {
        var dateFrom = request.GetDate("dateFrom");
        var dateTo   = request.GetDate("dateTo");
        var status   = request.GetString("status");

        // Raw SQL via EF — or use LINQ. Up to the plugin.
        // This example is intentionally simplified.
        var sql = @"
            SELECT p.Name AS ProjectName,
                   COUNT(a.Id) AS ActivityCount,
                   MAX(a.CreatedAt) AS LastActivity
            FROM Project p
            LEFT JOIN Activity a ON a.ProjectId = p.Id
                AND (@DateFrom IS NULL OR a.CreatedAt >= @DateFrom)
                AND (@DateTo IS NULL OR a.CreatedAt < @DateTo)
                AND (@Status IS NULL OR @Status = 'All' OR a.Status = @Status)
            WHERE p.TenantId = @TenantId
            GROUP BY p.Id, p.Name
            ORDER BY ActivityCount DESC";

        var rows = await context.Database.SqlQueryRaw<ProjectActivityRow>(
            sql,
            new Microsoft.Data.SqlClient.SqlParameter("@TenantId", request.TenantId),
            new Microsoft.Data.SqlClient.SqlParameter("@DateFrom", (object?)dateFrom ?? DBNull.Value),
            new Microsoft.Data.SqlClient.SqlParameter("@DateTo",   (object?)dateTo   ?? DBNull.Value),
            new Microsoft.Data.SqlClient.SqlParameter("@Status",   (object?)status   ?? DBNull.Value))
            .ToListAsync();

        var columns = new[]
        {
            new ReportColumn("ProjectName",    "Project",          ColumnType.Text,     true),
            new ReportColumn("ActivityCount",  "Activities",       ColumnType.Number,   true),
            new ReportColumn("LastActivity",   "Last activity",    ColumnType.DateTime, true)
        };

        var resultRows = rows.Select(r =>
            (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["ProjectName"]   = r.ProjectName,
                ["ActivityCount"] = r.ActivityCount,
                ["LastActivity"]  = r.LastActivity
            }).ToList();

        var totalActivities = rows.Sum(r => r.ActivityCount);
        var summary = new Dictionary<string, object?>
        {
            ["ProjectName"]   = "Total",
            ["ActivityCount"] = totalActivities,
            ["LastActivity"]  = null
        };

        return new ReportResult
        {
            Columns  = columns,
            Rows     = resultRows,
            Summary  = summary,
            ChartHint = new ReportChartHint("ProjectName", "ActivityCount", "bar")
        };
    }

    private record ProjectActivityRow(string ProjectName, int ActivityCount, DateTime? LastActivity);
}
