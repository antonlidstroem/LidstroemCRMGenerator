using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.RBAC.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Infrastructure.RBAC;

public class RbacModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permission");
            entity.HasIndex(p => p.Name)
                  .IsUnique()
                  .HasDatabaseName("UIX_Permission_Name");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");
            entity.HasIndex(r => new { r.TenantId, r.Name })
                  .IsUnique()
                  .HasDatabaseName("UIX_Role_TenantId_Name");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermission");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            entity.HasOne(rp => rp.Role)
                  .WithMany(r => r.RolePermissions)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rp => rp.Permission)
                  .WithMany(p => p.RolePermissions)
                  .HasForeignKey(rp => rp.PermissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActorRoleAssignment>(entity =>
        {
            entity.ToTable("ActorRoleAssignment");
            entity.HasIndex(a => new { a.TenantId, a.ActorId, a.RoleId })
                  .IsUnique()
                  .HasDatabaseName("UIX_ActorRoleAssignment");
            entity.HasOne(a => a.Role)
                  .WithMany(r => r.ActorAssignments)
                  .HasForeignKey(a => a.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
