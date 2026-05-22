using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Invitations.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Invitations;

public class InvitationsModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("Invitation");
            entity.HasIndex(i => i.Token)
                  .IsUnique()
                  .HasDatabaseName("UIX_Invitation_Token");
            entity.HasIndex(i => new { i.TenantId, i.Email, i.Status })
                  .HasDatabaseName("IX_Invitation_TenantId_Email_Status");
            entity.Property(i => i.Email).HasMaxLength(320);
            entity.Property(i => i.Token).HasMaxLength(128);
            entity.Property(i => i.RoleName).HasMaxLength(100);
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}

public class InvitationsPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("Invitations.Send", "Send invitations", "Invite users",       "Invitations"),
        new PermissionDefinition("Invitations.View", "View invitations", "List invitations",   "Invitations"),
    };
}
