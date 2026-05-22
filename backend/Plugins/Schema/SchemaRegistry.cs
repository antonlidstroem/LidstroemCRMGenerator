using Lidstroem.Core.Schema;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scrutor;

namespace Lidstroem.Plugins.Schema;

public class SchemaRegistry
{
    private readonly IEnumerable<ISchemaProvider> _providers;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SchemaRegistry> _logger;
    private const string CacheKey = "schema:all";

    public SchemaRegistry(
        IEnumerable<ISchemaProvider> providers,
        IMemoryCache cache,
        ILogger<SchemaRegistry> logger)
    {
        _providers = providers;
        _cache = cache;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, EntitySchema> GetAll()
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, EntitySchema>? cached)
         && cached != null)
            return cached;

        var schemas = new Dictionary<string, EntitySchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            foreach (var schema in provider.GetSchemas())
            {
                if (schemas.ContainsKey(schema.EntityType))
                    _logger.LogWarning("[Schema] Duplicate EntityType: {Type}", schema.EntityType);
                schemas[schema.EntityType] = schema;
            }
        }

        var result = (IReadOnlyDictionary<string, EntitySchema>)schemas;
        _cache.Set(CacheKey, result, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
        return result;
    }

    public EntitySchema? Get(string entityType) =>
        GetAll().TryGetValue(entityType, out var schema) ? schema : null;

    public IReadOnlyCollection<string> GetRegisteredTypes() =>
        GetAll().Keys.ToList().AsReadOnly();
}

public static class SchemaExtensions
{
    public static IServiceCollection AddSchemaRegistry(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<SchemaRegistry>();
        services.AddScoped<PluginManifestService>();
        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<ISchemaProvider>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());
        return services;
    }
}

// Extension to register PluginManifestService alongside the schema registry
