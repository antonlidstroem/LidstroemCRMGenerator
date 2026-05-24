using System.Reflection;
using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        _tenantContext = null;
    }

    public override int SaveChanges()
    {
        StampTenantId();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantId();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTenantId()
    {
        if (_tenantContext == null || _tenantContext.IsSystemContext) return;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = _tenantContext.TenantId;

            if (entry.Entity.OwnerId == null && _tenantContext.OwnerId.HasValue)
                entry.Entity.OwnerId = _tenantContext.OwnerId;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        LoadPluginAssemblies();
        ConfigureEntities(modelBuilder);
        RunPluginConfigurators(modelBuilder);
    }

    private static readonly ILogger _pluginLoadLogger =
        LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AppDbContext>();

    private static void LoadPluginAssemblies()
    {
        var foldersToScan = new HashSet<string>
        {
            AppDomain.CurrentDomain.BaseDirectory
        };

        var execLoc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (execLoc != null) foldersToScan.Add(execLoc);

        var entryLoc = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
        if (entryLoc != null) foldersToScan.Add(entryLoc);

        foreach (var folder in foldersToScan)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var dll in Directory.GetFiles(folder, "Lidstroem*.dll"))
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(dll);
                    if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName == name.FullName))
                        Assembly.LoadFrom(dll);
                }
                catch (Exception ex) when (ex is BadImageFormatException
                                              or FileLoadException
                                              or FileNotFoundException)
                {
                    _pluginLoadLogger.LogWarning(ex, "Failed to load optional plugin assembly {Dll}", dll);
                }
            }
        }
    }

    private void ConfigureEntities(ModelBuilder modelBuilder)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("Lidstroem") == true);

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly)
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BaseEntity))))
            {
                modelBuilder.Entity(type).ToTable(type.Name);

                typeof(AppDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(type)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private static void RunPluginConfigurators(ModelBuilder modelBuilder)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("Lidstroem") == true);

        foreach (var assembly in assemblies)
        {
            foreach (var configType in GetLoadableTypes(assembly)
                .Where(t => typeof(IPluginModelConfigurator).IsAssignableFrom(t)
                         && !t.IsInterface && !t.IsAbstract))
            {
                var configurator = (IPluginModelConfigurator)Activator.CreateInstance(configType)!;
                configurator.ConfigureModel(modelBuilder);
            }
        }
    }

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : BaseEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(
            e => _tenantContext == null
              || _tenantContext.IsSystemContext
              || e.TenantId == _tenantContext.TenantId);
    }
    /// <summary>
    /// Safe alternative to Assembly.GetTypes() that tolerates partial-load failures.
    /// When a plugin assembly references a type that isn't present at runtime,
    /// GetTypes() throws ReflectionTypeLoadException — this returns only the types
    /// that did load successfully, so one bad plugin can't take down the whole app.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

}
