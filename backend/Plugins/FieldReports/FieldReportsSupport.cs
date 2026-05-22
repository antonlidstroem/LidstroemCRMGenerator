using Lidstroem.Core.Constants;
using Lidstroem.Core.GDPR;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.FieldReports.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.FieldReports;

public class FieldReportModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FieldReport>(entity =>
        {
            entity.ToTable("FieldReport");
            entity.HasIndex(r => new { r.TenantId, r.ActivityType, r.ActivityId })
                  .HasDatabaseName("IX_FieldReport_Activity");
            entity.HasIndex(r => new { r.TenantId, r.ContextType, r.ContextId })
                  .HasDatabaseName("IX_FieldReport_Context");
            entity.HasIndex(r => r.AuthorActorId)
                  .HasDatabaseName("IX_FieldReport_AuthorActorId");
        });

        modelBuilder.Entity<FieldReportContributor>(entity =>
        {
            entity.ToTable("FieldReportContributor");
            entity.HasKey(c => new { c.FieldReportId, c.ActorId });
            entity.HasOne(c => c.FieldReport)
                  .WithMany(r => r.Contributors)
                  .HasForeignKey(c => c.FieldReportId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class FieldReportPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("FieldReports.View",   "View field reports",   "Read and list field reports",  "FieldReports"),
        new PermissionDefinition("FieldReports.Create", "Create field report",  "Create new field reports",     "FieldReports"),
        new PermissionDefinition("FieldReports.Edit",   "Edit field report",    "Update field reports",         "FieldReports"),
        new PermissionDefinition("FieldReports.Delete", "Delete field report",  "Remove field reports",         "FieldReports"),
    };
}

public class FieldReportPluginMetadata : IPluginMetadata
{
    public string PluginKey => "FieldReports";
    public string RoutePrefix => "fieldreports";
}

public class FieldReportGdprHandler : IGdprHandler
{
    private readonly DbContext _context;
    public string HandlerName => "FieldReports";

    public FieldReportGdprHandler(DbContext context) => _context = context;

    public async Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId, string subjectType, Guid tenantId, CancellationToken ct = default)
    {
        if (!string.Equals(subjectType, "Actor", StringComparison.OrdinalIgnoreCase))
            return GdprHandlerResult.Skipped(HandlerName);

        try
        {
            var affected = 0;

            var authored = await _context.Set<FieldReport>().IgnoreQueryFilters()
                .Where(r => r.AuthorActorId == subjectId && r.TenantId == tenantId)
                .ToListAsync(ct);

            foreach (var r in authored)
            {
                r.AuthorActorId = ActorConstants.AnonymousActorId;
                affected++;
            }

            // FieldReportContributor has no TenantId of its own (no BaseEntity).
            // We must IgnoreQueryFilters here since the EF global query filter would
            // otherwise filter through the join to FieldReport.TenantId, but
            // FieldReportContributor itself is not in the filter chain.
            // The manual TenantId check on FieldReport.TenantId is handled above via
            // the authored query; here we delete by ActorId across all tenants the actor
            // belongs to (the contributor table has no tenant — it inherits from FieldReport).
            var contributorRows = await _context.Set<FieldReportContributor>()
                .Where(c => c.ActorId == subjectId)
                .ToListAsync(ct);

            _context.Set<FieldReportContributor>().RemoveRange(contributorRows);
            affected += contributorRows.Count;

            await _context.SaveChangesAsync(ct);
            return GdprHandlerResult.Ok(HandlerName, affected);
        }
        catch (Exception ex)
        {
            return GdprHandlerResult.Failed(HandlerName, ex.Message);
        }
    }
}

public class ActorFieldReportExtensionProvider : IEntityExtensionProvider
{
    public string TargetEntityName => "Actor";

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context)
    {
        var authored = await context.Set<FieldReport>()
            .Where(r => r.AuthorActorId == entityId)
            .ToListAsync();

        var contributed = await context.Set<FieldReportContributor>()
            .Where(c => c.ActorId == entityId)
            .Select(c => c.FieldReportId)
            .ToListAsync();

        return new { Authored = authored, ContributedToIds = contributed };
    }
}
