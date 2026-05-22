using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.SuperAdmin.Entities;
using Lidstroem.Plugins.SuperAdmin.Middleware;
using Lidstroem.Plugins.SuperAdmin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lidstroem.Plugins.SuperAdmin;

public class SuperAdminModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenant");
            entity.HasIndex(t => t.ExternalId).IsUnique().HasDatabaseName("UIX_Tenant_ExternalId");
            entity.HasIndex(t => t.Name).IsUnique().HasDatabaseName("UIX_Tenant_Name");
            entity.HasQueryFilter(t => true); // exempt from global tenant filter
        });

        modelBuilder.Entity<TenantPluginAssignment>(entity =>
        {
            entity.ToTable("TenantPluginAssignment");
            entity.HasIndex(a => new { a.TenantEntityId, a.PluginKey })
                  .IsUnique().HasDatabaseName("UIX_TenantPluginAssignment");
            entity.HasOne(a => a.TenantEntity)
                  .WithMany(t => t.PluginAssignments)
                  .HasForeignKey(a => a.TenantEntityId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantCustomPage>(entity =>
        {
            entity.ToTable("TenantCustomPage");
            entity.HasIndex(a => new { a.TenantEntityId, a.PageKey })
                  .IsUnique().HasDatabaseName("UIX_TenantCustomPage");
            entity.HasOne(a => a.TenantEntity)
                  .WithMany(t => t.CustomPages)
                  .HasForeignKey(a => a.TenantEntityId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.ToTable("SystemLog");
            entity.HasIndex(l => new { l.LoggedAt, l.Level }).HasDatabaseName("IX_SystemLog_LoggedAt_Level");
            entity.Property(l => l.Level).HasMaxLength(20);
            entity.Property(l => l.Category).HasMaxLength(200);
        });
    }
}

public class SuperAdminPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("SuperAdmin.ManageTenants",      "Manage tenants",         "Create and manage tenants",      "SuperAdmin"),
        new PermissionDefinition("SuperAdmin.ManagePlugins",      "Manage plugins",         "Enable/disable plugins",         "SuperAdmin"),
        new PermissionDefinition("SuperAdmin.ViewHealth",         "System health",          "View system status",             "SuperAdmin"),
        new PermissionDefinition("SuperAdmin.ViewAllTenants",     "Cross-tenant read",      "Read data from all tenants",     "SuperAdmin"),
        new PermissionDefinition("SuperAdmin.ManageCredentials",  "Manage credentials",     "Create/update actor credentials","SuperAdmin"),
    };
}

public static class SuperAdminExtensions
{
    public static IServiceCollection AddSuperAdmin(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHostedService<SystemSeedService>();
        return services;
    }

    public static IApplicationBuilder UseSuperAdmin(this IApplicationBuilder app)
    {
        app.UseMiddleware<PluginActivationMiddleware>();
        return app;
    }
}
