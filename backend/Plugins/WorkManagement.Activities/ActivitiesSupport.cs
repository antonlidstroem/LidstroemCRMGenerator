using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.WorkManagement.Activities.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.WorkManagement.Activities;

public class ActivityModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("Activity");

            // ProjectId is a required FK — enforced here so the DB reflects the constraint.
            // No navigation property to Project to avoid cross-plugin coupling.
            entity.Property(a => a.ProjectId).IsRequired();
            entity.HasIndex(a => a.ProjectId).HasDatabaseName("IX_Activity_ProjectId");
            entity.HasIndex(a => new { a.TenantId, a.ProjectId }).HasDatabaseName("IX_Activity_Tenant_Project");
        });

        modelBuilder.Entity<ActivityActor>(entity =>
        {
            entity.ToTable("ActivityActor");
            entity.HasKey(aa => new { aa.ActivityId, aa.ActorId });
            entity.HasOne(aa => aa.Activity)
                  .WithMany(a => a.InvolvedActors)
                  .HasForeignKey(aa => aa.ActivityId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class ActivityPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("Activities.View",   "View activities",   "List and read activities", "WorkManagement.Activities"),
        new PermissionDefinition("Activities.Create", "Create activity",   "Create new activity",      "WorkManagement.Activities"),
        new PermissionDefinition("Activities.Edit",   "Edit activity",     "Update activity",          "WorkManagement.Activities"),
        new PermissionDefinition("Activities.Delete", "Delete activity",   "Remove activity",          "WorkManagement.Activities"),
    };
}

public class ActivityPluginMetadata : IPluginMetadata
{
    public string PluginKey => "WorkManagement.Activities";
    public string RoutePrefix => "activities";
}

public class ActivityLinkResolver : ILinkResolver
{
    public string TargetType => "Activity";

    public async Task<string?> ResolveNameAsync(int targetId, DbContext context)
    {
        var activity = await context.Set<Activity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == targetId);
        return activity?.Title;
    }
}

/// <summary>
/// Extends Project detail responses with their activities.
/// Registered via DI so the Project detail page shows activities without
/// the Projects plugin knowing Activities exist.
/// </summary>
public class ProjectActivityExtensionProvider : IEntityExtensionProvider
{
    public string TargetEntityName => "Project";

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context) =>
        await context.Set<Activity>()
            .Where(a => a.ProjectId == entityId)
            .OrderBy(a => a.StartDate)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                a.StartDate,
                a.EndDate,
                InvolvedActorCount = a.InvolvedActors.Count
            })
            .ToListAsync();
}

/// <summary>
/// Extends Activity detail with the list of involved actor IDs.
/// </summary>
public class ActivityActorExtensionProvider : IEntityExtensionProvider
{
    public string TargetEntityName => "Activity";

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context) =>
        await context.Set<ActivityActor>()
            .Where(aa => aa.ActivityId == entityId)
            .Select(aa => aa.ActorId)
            .ToListAsync();
}
