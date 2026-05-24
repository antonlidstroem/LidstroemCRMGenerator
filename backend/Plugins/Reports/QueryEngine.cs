using System.Reflection;
using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Schema;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Reports;

/// <summary>
/// Executes ad-hoc aggregate queries against any registered entity type.
/// Called by QueryController — never used directly by plugins.
///
/// Validates all field names against the entity's EntitySchema before touching
/// the database, so callers can't query arbitrary columns.
/// </summary>
public class QueryEngine
{
    private readonly DbContext _context;

    public QueryEngine(DbContext context) => _context = context;

    public async Task<QueryResult> RunAsync(QueryRequest request, EntitySchema schema)
    {
        // Resolve the CLR type for the entity
        var entityType = ResolveEntityType(schema.EntityType);
        if (entityType == null)
            throw new InvalidOperationException(
                $"CLR type not found for entity '{schema.EntityType}'.");

        // Validate all referenced fields exist in the schema
        ValidateFields(request, schema);

        // Build and execute via reflection — EF's DbContext.Set<T>() needs a generic param
        var method = typeof(QueryEngine)
            .GetMethod(nameof(RunTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        return await (Task<QueryResult>)method.Invoke(this, new object[] { request })!;
    }

    private async Task<QueryResult> RunTypedAsync<T>(QueryRequest request)
        where T : BaseEntity
    {
        // Load into memory first — filters use reflection-based predicates
        // which can't be translated to SQL anyway. For our data volumes this is fine.
        var allItems = await _context.Set<T>().ToListAsync();
        IEnumerable<T> query = allItems;

        // ── Filters ───────────────────────────────────────────────────────────
        foreach (var filter in request.Filters ?? Enumerable.Empty<QueryFilter>())
        {
            var prop = typeof(T).GetProperty(
                filter.Field, BindingFlags.Public | BindingFlags.Instance |
                              BindingFlags.IgnoreCase);
            if (prop == null) continue;

            query = filter.Op switch
            {
                FilterOp.Eq  => query.Where(e => Equals(GetPropertyValue(e, prop.Name)?.ToString(), filter.Value)),
                FilterOp.Neq => query.Where(e => !Equals(GetPropertyValue(e, prop.Name)?.ToString(), filter.Value)),
                FilterOp.Gt  => query.Where(e => CompareValues(GetPropertyValue(e, prop.Name), filter.Value) > 0),
                FilterOp.Gte => query.Where(e => CompareValues(GetPropertyValue(e, prop.Name), filter.Value) >= 0),
                FilterOp.Lt  => query.Where(e => CompareValues(GetPropertyValue(e, prop.Name), filter.Value) < 0),
                FilterOp.Lte => query.Where(e => CompareValues(GetPropertyValue(e, prop.Name), filter.Value) <= 0),
                FilterOp.Contains => query.Where(e =>
                    (GetPropertyValue(e, prop.Name)?.ToString() ?? string.Empty)
                    .Contains(filter.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase)),
                _ => query
            };
        }

        // ── Date range ────────────────────────────────────────────────────────
        if (request.DateRange != null)
        {
            var dr = request.DateRange;
            var dateProp = typeof(T).GetProperty(
                dr.Field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (dateProp != null)
            {
                if (dr.From.HasValue)
                {
                    var from = dr.From.Value.ToString("O");
                    query = query.Where(e => CompareValues(GetPropertyValue(e, dateProp.Name), from) >= 0);
                }
                if (dr.To.HasValue)
                {
                    var to = dr.To.Value.AddDays(1).ToString("O");
                    query = query.Where(e => CompareValues(GetPropertyValue(e, dateProp.Name), to) < 0);
                }
            }
        }

        var items = query
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Min(request.Limit ?? 500, 2000))
            .ToList();

        // ── No grouping: return raw list with requested fields ─────────────────
        if (string.IsNullOrEmpty(request.GroupBy))
        {
            var columns = (request.Fields ?? new List<string>())
                .Select(f => new QueryResultColumn(f, f, GuessColumnType(typeof(T), f)))
                .ToList();

            var rows = items.Select(item =>
                (IReadOnlyDictionary<string, object?>)columns.ToDictionary(
                    c => c.Key,
                    c => (object?)GetPropertyValue(item, c.Key)));

            return new QueryResult(columns, rows.ToList(), null);
        }

        // ── Grouped + aggregated ──────────────────────────────────────────────
        var groupProp = typeof(T).GetProperty(
            request.GroupBy,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (groupProp == null)
            throw new InvalidOperationException(
                $"GroupBy field '{request.GroupBy}' not found on '{typeof(T).Name}'.");

        var rawItems = items;

        var grouped = rawItems
            .GroupBy(e => GetPropertyValue(e, groupProp.Name)?.ToString() ?? "(none)")
            .Select(g =>
            {
                var row = new Dictionary<string, object?> { [request.GroupBy] = g.Key };

                foreach (var agg in request.Aggregate ?? Enumerable.Empty<AggregateSpec>())
                {
                    var vals = g
                        .Select(e => GetPropertyValue(e, agg.Field))
                        .Where(v => v != null)
                        .ToList();

                    row[GetAggKey(agg)] = agg.Fn switch
                    {
                        AggregateFn.Count => (object?)g.Count(),
                        AggregateFn.Sum   => TrySum(vals),
                        AggregateFn.Avg   => TryAvg(vals),
                        AggregateFn.Min   => vals.Count > 0 ? vals.Min() : null,
                        AggregateFn.Max   => vals.Count > 0 ? vals.Max() : null,
                        _ => null
                    };
                }

                return (IReadOnlyDictionary<string, object?>)row;
            })
            .ToList();

        // Build column list from GroupBy + aggregates
        var resultColumns = new List<QueryResultColumn>
        {
            new(request.GroupBy, request.GroupBy, QueryColumnType.Text)
        };

        foreach (var agg in request.Aggregate ?? Enumerable.Empty<AggregateSpec>())
        {
            var colType = agg.Fn == AggregateFn.Count ? QueryColumnType.Number : QueryColumnType.Decimal;
            resultColumns.Add(new QueryResultColumn(GetAggKey(agg), GetAggKey(agg), colType));
        }

        // Summary row
        IReadOnlyDictionary<string, object?>? summary = null;
        if (grouped.Count > 1 && (request.Aggregate?.Any() ?? false))
        {
            var summaryRow = new Dictionary<string, object?> { [request.GroupBy] = "Total" };
            foreach (var agg in request.Aggregate!)
            {
                var key = GetAggKey(agg);
                var colVals = grouped.Select(r => r.TryGetValue(key, out var v) ? v : null).ToList();
                summaryRow[key] = agg.Fn == AggregateFn.Count
                    ? (object?)colVals.Sum(v => v is int i ? i : 0)
                    : TrySum(colVals!);
            }
            summary = summaryRow;
        }

        return new QueryResult(resultColumns, grouped, summary);
    }

    // ── Field validation ──────────────────────────────────────────────────────

    private static void ValidateFields(QueryRequest request, EntitySchema schema)
    {
        var validFields = schema.Fields.Select(f => f.FieldName.ToLowerInvariant()).ToHashSet();
        // Also allow BaseEntity fields
        validFields.UnionWith(new[] { "id", "createdat", "updatedat", "ownerid" });

        var allRequestedFields = new List<string>();
        if (!string.IsNullOrEmpty(request.GroupBy)) allRequestedFields.Add(request.GroupBy);
        allRequestedFields.AddRange(request.Fields ?? Enumerable.Empty<string>());
        allRequestedFields.AddRange(request.Aggregate?.Select(a => a.Field) ?? Enumerable.Empty<string>());
        allRequestedFields.AddRange(request.Filters?.Select(f => f.Field) ?? Enumerable.Empty<string>());
        if (request.DateRange != null) allRequestedFields.Add(request.DateRange.Field);

        foreach (var field in allRequestedFields)
        {
            if (!validFields.Contains(field.ToLowerInvariant()))
                throw new InvalidOperationException(
                    $"Field '{field}' is not defined in schema '{schema.EntityType}'. " +
                    $"Valid fields: {string.Join(", ", schema.Fields.Select(f => f.FieldName))}");
        }
    }

    // ── Type resolution ───────────────────────────────────────────────────────

    private Type? ResolveEntityType(string entityTypeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("Lidstroem") == true)
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
            .FirstOrDefault(t => t.Name == entityTypeName
                              && typeof(BaseEntity).IsAssignableFrom(t)
                              && !t.IsAbstract);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Cache property lookups per type to avoid repeated reflection on hot paths.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), PropertyInfo?>
        _propCache = new();

    private static object? GetPropertyValue(object obj, string propName)
    {
        var prop = _propCache.GetOrAdd(
            (obj.GetType(), propName),
            key => key.Item1.GetProperty(
                key.Item2,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));
        return prop?.GetValue(obj);
    }

    private static string GetAggKey(AggregateSpec agg) =>
        $"{agg.Fn.ToString().ToLower()}_{agg.Field}";

    private static object? TrySum(IList<object?> vals)
    {
        try { return vals.Sum(v => Convert.ToDecimal(v)); }
        catch { return null; }
    }

    private static object? TryAvg(IList<object?> vals)
    {
        try { return vals.Count > 0 ? vals.Average(v => Convert.ToDecimal(v)) : null; }
        catch { return null; }
    }

    private static QueryColumnType GuessColumnType(Type entityType, string fieldName)
    {
        var prop = entityType.GetProperty(fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null) return QueryColumnType.Text;
        var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (t == typeof(int) || t == typeof(long)) return QueryColumnType.Number;
        if (t == typeof(decimal) || t == typeof(double) || t == typeof(float)) return QueryColumnType.Decimal;
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return QueryColumnType.DateTime;
        return QueryColumnType.Text;
    }

    // Compares two values numerically if possible, otherwise as strings.
    private static int CompareValues(object? left, string? right)
    {
        if (left == null || right == null)
            return left == null && right == null ? 0 : (left == null ? -1 : 1);
        try
        {
            var l = Convert.ToDouble(left);
            var r = Convert.ToDouble(right);
            return l.CompareTo(r);
        }
        catch
        {
            return string.Compare(left.ToString(), right, StringComparison.Ordinal);
        }
    }
}
