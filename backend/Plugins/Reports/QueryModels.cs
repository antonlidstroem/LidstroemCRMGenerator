namespace Lidstroem.Plugins.Reports;

// ── Request ───────────────────────────────────────────────────────────────────

public class QueryRequest
{
    /// <summary>Entity type to query, e.g. "Donation". Must match a registered EntitySchema.</summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>Optional: specific fields to return when not grouping.</summary>
    public List<string>? Fields { get; init; }

    /// <summary>Optional: field to group by, e.g. "projectId".</summary>
    public string? GroupBy { get; init; }

    /// <summary>Aggregate functions to apply per group (or overall).</summary>
    public List<AggregateSpec>? Aggregate { get; init; }

    /// <summary>Row-level filters applied before grouping.</summary>
    public List<QueryFilter>? Filters { get; init; }

    /// <summary>Optional date range filter.</summary>
    public DateRangeFilter? DateRange { get; init; }

    /// <summary>Max rows returned for non-grouped queries. Capped at 2000.</summary>
    public int? Limit { get; init; }
}

public record AggregateSpec(string Field, AggregateFn Fn);

public enum AggregateFn { Count, Sum, Avg, Min, Max }

public class QueryFilter
{
    public string Field { get; init; } = string.Empty;
    public FilterOp Op { get; init; } = FilterOp.Eq;
    public string? Value { get; init; }
}

public enum FilterOp { Eq, Neq, Gt, Gte, Lt, Lte, Contains }

public class DateRangeFilter
{
    public string Field { get; init; } = "createdAt";
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

// ── Response ──────────────────────────────────────────────────────────────────

public class QueryResult
{
    public IReadOnlyList<QueryResultColumn> Columns { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public IReadOnlyDictionary<string, object?>? Summary { get; init; }
    public int TotalRows => Rows.Count;

    public QueryResult(
        IReadOnlyList<QueryResultColumn> columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyDictionary<string, object?>? summary)
    {
        Columns = columns;
        Rows = rows;
        Summary = summary;
    }
}

public record QueryResultColumn(string Key, string DisplayName, QueryColumnType Type);

public enum QueryColumnType { Text, Number, Decimal, DateTime, Currency, Percent }
