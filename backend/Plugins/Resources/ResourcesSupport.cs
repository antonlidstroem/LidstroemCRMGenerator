using Lidstroem.Core.GDPR;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Resources.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lidstroem.Plugins.Resources;

public class ResourceModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("Resource");
            entity.HasIndex(r => new { r.TargetType, r.TargetId })
                  .HasDatabaseName("IX_Resource_Target");
            entity.HasIndex(r => new { r.TenantId, r.TargetType, r.TargetId })
                  .HasDatabaseName("IX_Resource_TenantId_Target");
            entity.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.StoragePath).HasMaxLength(1000);
            entity.Property(r => r.ExternalUrl).HasMaxLength(2000);
            entity.Property(r => r.ContentType).HasMaxLength(200);
        });
    }
}

public class ResourcePermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("Resources.View",   "View resources",   "Download and list resources", "Resources"),
        new PermissionDefinition("Resources.Upload", "Upload resources", "Attach files and links",      "Resources"),
        new PermissionDefinition("Resources.Delete", "Delete resources", "Remove attached resources",   "Resources"),
    };
}

public class ResourcePluginMetadata : IPluginMetadata
{
    public string PluginKey => "Resources";
    public string RoutePrefix => "resources";
}

public class ResourceGdprHandler : IGdprHandler
{
    private readonly DbContext _context;
    private readonly IStorageProvider _storage;
    public string HandlerName => "Resources";

    public ResourceGdprHandler(DbContext context, IStorageProvider storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId, string subjectType, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var resources = await _context.Set<Resource>().IgnoreQueryFilters()
                .Where(r => r.TargetType == subjectType
                         && r.TargetId == subjectId
                         && r.TenantId == tenantId)
                .ToListAsync(ct);

            foreach (var r in resources)
                if (r.StoragePath != null)
                    await _storage.DeleteAsync(r.StoragePath);

            _context.Set<Resource>().RemoveRange(resources);
            await _context.SaveChangesAsync(ct);
            return GdprHandlerResult.Ok(HandlerName, resources.Count);
        }
        catch (Exception ex)
        {
            return GdprHandlerResult.Failed(HandlerName, ex.Message);
        }
    }
}

/// <summary>
/// Generic extension provider — instantiate and register manually per entity type,
/// e.g. services.AddScoped&lt;IEntityExtensionProvider&gt;(_ => new GenericResourceExtensionProvider("Activity"));
/// Kept internal so Scrutor's assembly scan does not pick it up automatically
/// (its string constructor parameter cannot be resolved by the DI container).
/// </summary>
internal class GenericResourceExtensionProvider : IEntityExtensionProvider
{
    private readonly string _entityType;

    public GenericResourceExtensionProvider(string entityType) => _entityType = entityType;

    public string TargetEntityName => _entityType;

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context) =>
        await context.Set<Resource>()
            .Where(r => r.TargetType == _entityType && r.TargetId == entityId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
}

public static class ResourceExtensions
{
    public static IServiceCollection AddResources(
        this IServiceCollection services, IHostEnvironment env)
    {
        services.AddScoped<IStorageProvider, LocalDiskStorageProvider>();
        return services;
    }
}

public class LocalDiskStorageProvider : IStorageProvider
{
    private readonly string _basePath;

    public LocalDiskStorageProvider(IConfiguration config)
    {
        _basePath = config["Resources:Storage:LocalPath"]
            ?? Path.Combine(Path.GetTempPath(), "lidstroem-resources");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreAsync(Stream content, string fileName, string contentType)
    {
        var sanitized = string.Join("_",
            fileName.Split(Path.GetInvalidFileNameChars())).ToLowerInvariant();

        var relativePath = Path.Combine(
            DateTime.UtcNow.Year.ToString(),
            DateTime.UtcNow.Month.ToString("D2"),
            $"{Guid.NewGuid():N}_{sanitized}");

        var fullPath = Path.Combine(_basePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream);
        return relativePath;
    }

    public Task<Stream> RetrieveAsync(string storagePath)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Resource file not found: {storagePath}");
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string storagePath)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<string?> GetPublicUrlAsync(string storagePath) =>
        Task.FromResult<string?>(null);
}
