using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.WorkManagement.Projects.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.WorkManagement.Projects;

public class ProjectModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Project");
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("ProjectMember");
            entity.HasKey(pm => new { pm.ProjectId, pm.ActorId });
            entity.HasOne(pm => pm.Project)
                  .WithMany(p => p.Members)
                  .HasForeignKey(pm => pm.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class ProjectPermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("Projects.View",          "View projects",      "List and read projects",       "WorkManagement.Projects"),
        new PermissionDefinition("Projects.Create",        "Create project",     "Create new project",           "WorkManagement.Projects"),
        new PermissionDefinition("Projects.Edit",          "Edit project",       "Update project",               "WorkManagement.Projects"),
        new PermissionDefinition("Projects.Delete",        "Delete project",     "Remove project",               "WorkManagement.Projects"),
        new PermissionDefinition("Projects.ManageMembers", "Manage members",     "Add/remove project members",   "WorkManagement.Projects"),
    };
}

public class ProjectPluginMetadata : IPluginMetadata
{
    public string PluginKey => "WorkManagement.Projects";
    public string RoutePrefix => "projects";
}

public class ProjectLinkResolver : ILinkResolver
{
    public string TargetType => "Project";

    public async Task<string?> ResolveNameAsync(int targetId, DbContext context)
    {
        var project = await context.Set<Project>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == targetId);
        return project?.Title;
    }
}
