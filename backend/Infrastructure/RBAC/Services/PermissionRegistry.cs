using Lidstroem.Core.Constants;
using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.Data;
using Lidstroem.Infrastructure.RBAC.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Infrastructure.RBAC.Services;

/// <summary>
/// Runs on startup, collects all IPermissionProvider registrations
/// and ensures the Permission table is up to date.
/// Skips gracefully if the database hasn't been migrated yet.
/// </summary>
public class PermissionRegistry : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PermissionRegistry> _logger;

    public PermissionRegistry(IServiceProvider services, ILogger<PermissionRegistry> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Apply any pending migrations automatically on startup.
        // This also ensures the database exists before we query it.
        try
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RBAC] Database migration failed. PermissionRegistry will not run.");
            return;
        }

        var providers = scope.ServiceProvider.GetServices<IPermissionProvider>();

        var declared = providers
            .SelectMany(p => p.GetPermissions())
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToList();

        List<Permission> existing;
        try
        {
            existing = await context.Set<Permission>()
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RBAC] Could not read Permission table. Has the database been migrated?");
            return;
        }

        foreach (var def in declared)
        {
            var existing_ = existing.FirstOrDefault(e => e.Name == def.Name);
            if (existing_ == null)
            {
                context.Set<Permission>().Add(new Permission
                {
                    Name = def.Name,
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    Category = def.Category,
                    IsActive = true,
                    TenantId = TenantConstants.SystemTenantId
                });
                _logger.LogInformation("[RBAC] New permission registered: {Name}", def.Name);
            }
            else if (!existing_.IsActive)
            {
                existing_.IsActive = true;
            }
        }

        var declaredNames = declared.Select(d => d.Name).ToHashSet();
        foreach (var obsolete in existing.Where(e => e.IsActive && !declaredNames.Contains(e.Name)))
        {
            obsolete.IsActive = false;
            _logger.LogWarning("[RBAC] Permission deactivated (no longer declared): {Name}", obsolete.Name);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
