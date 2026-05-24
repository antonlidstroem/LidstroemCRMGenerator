using System.Text.Json;
using Lidstroem.Frontend.Core.Models;

namespace Lidstroem.Frontend.Core.Services;

public class SchemaService
{
    // Fix 12: Replaced raw HttpClient with ApiClient so the /api/schema request
    // carries the Authorization header. Without it, the schema endpoint returns 401
    // (it is not [AllowAnonymous]), the cache stays empty, and the entire dynamic
    // UI renders nothing — every entity page shows "Plugin not available".
    private readonly ApiClient _api;
    private Dictionary<string, EntitySchema>? _cache;

    public SchemaService(ApiClient api) => _api = api;

    public async Task<IReadOnlyDictionary<string, EntitySchema>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        // /api/schema returns a JSON object keyed by EntityType.
        // GetOneAsync is the correct call — GetListAsync was previously called first
        // and then discarded, making two unnecessary HTTP requests on every cache miss.
        var obj = await _api.GetOneAsync("/api/schema");

        _cache = new Dictionary<string, EntitySchema>(StringComparer.OrdinalIgnoreCase);

        if (obj.HasValue && obj.Value.ValueKind == JsonValueKind.Object)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            foreach (var prop in obj.Value.EnumerateObject())
            {
                var schema = prop.Value.Deserialize<EntitySchema>(options);
                if (schema != null)
                    _cache[prop.Name] = schema;
            }
        }

        return _cache;
    }

    public async Task<EntitySchema?> GetAsync(string entityType)
    {
        var all = await GetAllAsync();
        return all.TryGetValue(entityType, out var schema) ? schema : null;
    }

    public async Task<IReadOnlyList<NavGroup>> GetNavGroupsAsync()
    {
        var all = await GetAllAsync();

        return all.Values
            .OrderBy(s => s.NavGroup ?? string.Empty)
            .ThenBy(s => s.NavOrder)
            .GroupBy(s => s.NavGroup)
            .Select(g => new NavGroup(
                g.Key,
                g.OrderBy(s => s.NavOrder).ToList().AsReadOnly()))
            .ToList()
            .AsReadOnly();
    }

    public void Invalidate() => _cache = null;
}
