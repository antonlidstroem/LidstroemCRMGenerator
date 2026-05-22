using Lidstroem.Core.Schema;

namespace Lidstroem.Plugins.WorkManagement.Projects;

public class ProjectSchemaProvider : ISchemaProvider
{
    public IReadOnlyCollection<EntitySchema> GetSchemas() => new[]
    {
        new EntitySchema
        {
            EntityType = "Project",
            DisplayName = "Project",
            DisplayNamePlural = "Projects",
            Icon = "folder",
            ApiBasePath = "/api/projects",
            // BUG FIX #18e: OwnerPluginKey was missing.
            OwnerPluginKey = "WorkManagement.Projects",
            NavGroup = "Work Management",
            NavOrder = 20,
            DefaultListColumns = new[] { "Title", "Description", "CreatedAt" },
            Fields = new[]
            {
                new FieldDefinition { FieldName = "Title",       DisplayName = "Title",       Type = FieldType.Text,     IsRequired = true,  MaxLength = 300, SortOrder = 1, IsFilterable = true },
                new FieldDefinition { FieldName = "Description", DisplayName = "Description", Type = FieldType.RichText, IsRequired = false, SortOrder = 2,  ShowInList = true },
            },
            Actions = new[]
            {
                new ActionDefinition { ActionKey = "create", DisplayName = "New project", Icon = "plus",  HttpMethod = "POST",   UrlTemplate = "/api/projects",     Placement = ActionPlacement.Toolbar },
                new ActionDefinition { ActionKey = "edit",   DisplayName = "Edit",        Icon = "edit",  HttpMethod = "PUT",    UrlTemplate = "/api/projects/{id}", Placement = ActionPlacement.Row },
                new ActionDefinition { ActionKey = "delete", DisplayName = "Delete",      Icon = "trash", HttpMethod = "DELETE", UrlTemplate = "/api/projects/{id}", RequiredPermission = "Projects.Delete", Placement = ActionPlacement.Row },
            }
        }
    };
}
