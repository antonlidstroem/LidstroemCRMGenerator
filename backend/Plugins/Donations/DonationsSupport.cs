using Lidstroem.Core.GDPR;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Donations.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Donations;

public class DonationModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Donation>(entity =>
        {
            entity.ToTable("Donation");
            entity.HasIndex(d => new { d.TenantId, d.DonorType, d.DonorId })
                  .HasDatabaseName("IX_Donation_Donor");
            entity.HasIndex(d => new { d.TenantId, d.TargetType, d.TargetId })
                  .HasDatabaseName("IX_Donation_Target");
            entity.Property(d => d.Currency).HasMaxLength(3);
            entity.Property(d => d.DonorType).HasMaxLength(50);
            entity.Property(d => d.TargetType).HasMaxLength(50);
        });
    }
}

public class DonationPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("Donations.View",   "View donations",   "List and read donations",  "Donations"),
        new PermissionDefinition("Donations.Create", "Create donation",  "Register new donation",    "Donations"),
        new PermissionDefinition("Donations.Edit",   "Edit donation",    "Update donation",          "Donations"),
        new PermissionDefinition("Donations.Delete", "Delete donation",  "Remove donation",          "Donations"),
    };
}

public class DonationPluginMetadata : IPluginMetadata
{
    public string PluginKey => "Donations";
    public string RoutePrefix => "donations";
}

public class DonationGdprHandler : IGdprHandler
{
    private readonly DbContext _context;
    public string HandlerName => "Donations";

    public DonationGdprHandler(DbContext context) => _context = context;

    public async Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId, string subjectType, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            // DonorType is stored as e.g. "Actor" (title-case). The subjectType parameter
            // arrives from the query string and may vary in casing. EF Core translates
            // string comparisons to case-insensitive SQL (COLLATE) on SQL Server by default,
            // so this is safe, but we make the intent explicit with OrdinalIgnoreCase on
            // in-memory comparisons for correctness in tests (SQLite is case-sensitive).
            var subjectTypeLower = subjectType.ToLowerInvariant();
            var donations = await _context.Set<Donation>().IgnoreQueryFilters()
                .Where(d => d.DonorType != null
                         && d.DonorType.ToLower() == subjectTypeLower
                         && d.DonorId == subjectId
                         && d.TenantId == tenantId)
                .ToListAsync(ct);

            foreach (var d in donations)
            {
                d.DonorId = null;
                d.DonorType = null;
            }

            await _context.SaveChangesAsync(ct);
            return GdprHandlerResult.Ok(HandlerName, donations.Count);
        }
        catch (Exception ex)
        {
            return GdprHandlerResult.Failed(HandlerName, ex.Message);
        }
    }
}

/// <summary>Extends Actor responses with donations where they are the donor.</summary>
public class ActorDonationExtensionProvider : IEntityExtensionProvider
{
    public string TargetEntityName => "Actor";

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context) =>
        await context.Set<Donation>()
            .Where(d => d.DonorType == "Actor" && d.DonorId == entityId)
            .OrderByDescending(d => d.DonationDate)
            .Select(d => new
            {
                d.Id, d.Amount, d.Currency, d.DonationDate, d.TargetType, d.TargetId
            })
            .ToListAsync();
}

/// <summary>Extends Project responses with all donations targeting that project.</summary>
public class ProjectDonationExtensionProvider : IEntityExtensionProvider
{
    public string TargetEntityName => "Project";

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context) =>
        await context.Set<Donation>()
            .Where(d => d.TargetType == "Project" && d.TargetId == entityId)
            .OrderByDescending(d => d.DonationDate)
            .Select(d => new
            {
                d.Id, d.Amount, d.Currency, d.DonationDate, d.DonorType, d.DonorId
            })
            .ToListAsync();
}

/// <summary>Extends Activity responses with all donations targeting that activity.</summary>
public class ActivityDonationExtensionProvider : IEntityExtensionProvider
{
    public string TargetEntityName => "Activity";

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context) =>
        await context.Set<Donation>()
            .Where(d => d.TargetType == "Activity" && d.TargetId == entityId)
            .OrderByDescending(d => d.DonationDate)
            .Select(d => new
            {
                d.Id, d.Amount, d.Currency, d.DonationDate, d.DonorType, d.DonorId
            })
            .ToListAsync();
}
