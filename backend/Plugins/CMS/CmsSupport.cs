using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.CMS.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.CMS;

public class CmsModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Page>(entity =>
        {
            entity.ToTable("Page");
            entity.HasIndex(p => new { p.TenantId, p.Slug })
                  .IsUnique()
                  .HasDatabaseName("UIX_Page_TenantId_Slug");
            entity.Property(p => p.Slug).HasMaxLength(200);
            entity.Property(p => p.Title).HasMaxLength(300);
            entity.Property(p => p.MetaDescription).HasMaxLength(160);
            entity.Property(p => p.Content).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<TenantSite>(entity =>
        {
            entity.ToTable("TenantSite");
            entity.HasIndex(s => s.Slug)
                  .IsUnique()
                  .HasDatabaseName("UIX_TenantSite_Slug");
            entity.Property(s => s.Slug).HasMaxLength(100);
            entity.Property(s => s.CustomDomain).HasMaxLength(253);
            entity.Property(s => s.ThemeName).HasMaxLength(50);
            entity.Property(s => s.SkinPackage).HasMaxLength(50);
            entity.Property(s => s.DarkMode).HasMaxLength(10);
            entity.Property(s => s.SkinJson).HasColumnType("nvarchar(max)");
        });
    }
}

public class CmsPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("CMS.View",    "View pages",    "List and read CMS pages",         "CMS"),
        new PermissionDefinition("CMS.Edit",    "Edit pages",    "Create and update CMS pages",     "CMS"),
        new PermissionDefinition("CMS.Publish", "Publish pages", "Publish and unpublish pages",     "CMS"),
    };
}

public class CmsPluginMetadata : IPluginMetadata
{
    public string PluginKey => "CMS";
    public string RoutePrefix => "cms";
}
