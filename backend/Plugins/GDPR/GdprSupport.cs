using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.GDPR.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using Lidstroem.Core.GDPR;

namespace Lidstroem.Plugins.GDPR;

public class GdprModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GdprLog>(entity =>
        {
            entity.ToTable("GdprLog");
            entity.HasIndex(g => g.EmailHash)
                  .HasDatabaseName("IX_GdprLog_EmailHash");
            entity.HasIndex(g => new { g.TenantId, g.SubjectType, g.ForgottenSubjectId })
                  .HasDatabaseName("IX_GdprLog_TenantId_Subject");
            entity.Property(g => g.EmailHash).HasMaxLength(64);
            entity.Property(g => g.SubjectType).HasMaxLength(100);
            entity.Property(g => g.ResultJson).HasColumnType("nvarchar(max)");
        });
    }
}

public class GdprPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("GDPR.Forget",  "Forget subject",   "Initiate GDPR deletion of a subject", "GDPR"),
        new PermissionDefinition("GDPR.ViewLog", "View GDPR log",    "Read the GDPR audit log",             "GDPR"),
    };
}

public static class GdprExtensions
{
    /// <summary>
    /// Scans all loaded assemblies for IGdprHandler implementations and registers them.
    /// Called from Program.cs so plugins don't need to register themselves.
    /// </summary>
    public static IServiceCollection AddGdpr(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<IGdprHandler>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
