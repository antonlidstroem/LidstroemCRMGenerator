using Lidstroem.Core.GDPR;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Communication.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Communication;

public class CommunicationModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("NotificationTemplate");
            entity.HasIndex(t => new { t.TemplateKey, t.TenantId })
                  .HasDatabaseName("IX_NotificationTemplate_Key_Tenant");
            entity.Property(t => t.TemplateKey).HasMaxLength(100);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");
            entity.HasIndex(n => new { n.TenantId, n.ActorId, n.IsRead })
                  .HasDatabaseName("IX_Notification_TenantId_ActorId_IsRead");
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("EmailLog");
            entity.HasIndex(e => new { e.TenantId, e.SentAt })
                  .HasDatabaseName("IX_EmailLog_TenantId_SentAt");
            entity.Property(e => e.ToAddress).HasMaxLength(320);
            entity.Property(e => e.TemplateKey).HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}

public class CommunicationPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("Communication.ViewInbox",       "View inbox",           "Read own notifications",        "Communication"),
        new PermissionDefinition("Communication.ManageTemplates", "Manage email templates","Create and edit templates",     "Communication"),
        new PermissionDefinition("Communication.ViewEmailLog",    "View email log",       "Read sent email log",           "Communication"),
    };
}

public class CommunicationPluginMetadata : IPluginMetadata
{
    public string PluginKey => "Communication";
    public string RoutePrefix => "notifications";
}

public class CommunicationGdprHandler : IGdprHandler
{
    private readonly DbContext _context;
    public string HandlerName => "Communication";

    public CommunicationGdprHandler(DbContext context) => _context = context;

    public async Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId, string subjectType, Guid tenantId, CancellationToken ct = default)
    {
        if (!string.Equals(subjectType, "Actor", StringComparison.OrdinalIgnoreCase))
            return GdprHandlerResult.Skipped(HandlerName);
        try
        {
            var notifications = await _context.Set<Notification>().IgnoreQueryFilters()
                .Where(n => n.ActorId == subjectId && n.TenantId == tenantId)
                .ToListAsync(ct);
            _context.Set<Notification>().RemoveRange(notifications);
            await _context.SaveChangesAsync(ct);
            return GdprHandlerResult.Ok(HandlerName, notifications.Count);
        }
        catch (Exception ex)
        {
            return GdprHandlerResult.Failed(HandlerName, ex.Message);
        }
    }
}
