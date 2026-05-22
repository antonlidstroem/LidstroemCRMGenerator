using Lidstroem.Core.Auth;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Infrastructure.Data;

/// <summary>
/// Configures the Core entities that are owned by Infrastructure.
/// This is the only configurator that touches Actor, ActorCredentials and RefreshToken.
/// </summary>
public class CoreModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Actor>(entity =>
        {
            entity.ToTable("Actor");
            entity.HasIndex(a => new { a.TenantId, a.Email })
                  .HasDatabaseName("IX_Actor_TenantId_Email");
            entity.Property(a => a.Email).HasMaxLength(320);
            entity.Property(a => a.DisplayName).HasMaxLength(200);
            entity.Property(a => a.PhoneNumber).HasMaxLength(50);
        });

        modelBuilder.Entity<ActorCredentials>(entity =>
        {
            entity.ToTable("ActorCredentials");
            entity.HasIndex(c => new { c.TenantId, c.Identifier })
                  .IsUnique()
                  .HasDatabaseName("UIX_ActorCredentials_TenantId_Identifier");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshToken");
            entity.HasIndex(r => r.Token)
                  .IsUnique()
                  .HasDatabaseName("UIX_RefreshToken_Token");
            entity.HasOne(r => r.Credentials)
                  .WithMany(c => c.RefreshTokens)
                  .HasForeignKey(r => r.ActorCredentialsId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
