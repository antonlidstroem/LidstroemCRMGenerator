using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.ACL.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.ACL;

public class AclModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AclGrant>(entity =>
        {
            entity.ToTable("AclGrant");
            entity.HasIndex(g => new { g.TenantId, g.GrantedToActorId, g.ResourceType, g.ResourceId, g.Action })
                  .HasDatabaseName("IX_AclGrant_Actor_Resource_Action");
            entity.HasIndex(g => new { g.TenantId, g.GrantedToActorId })
                  .HasDatabaseName("IX_AclGrant_GrantedTo");
            entity.Property(g => g.ResourceType).HasMaxLength(100);
            entity.Property(g => g.Action).HasConversion<string>().HasMaxLength(20);
        });
    }
}
