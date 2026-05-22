using Lidstroem.Core.Schema;

namespace Lidstroem.Plugins.WorkManagement.Activities;

public class ActivitySchemaProvider : ISchemaProvider
{
    public IReadOnlyCollection<EntitySchema> GetSchemas() => new[]
    {
        new EntitySchema
        {
            EntityType = "Activity",
            DisplayName = "Activity",
            DisplayNamePlural = "Activities",
            Icon = "calendar",
            ApiBasePath = "/api/activities",
            // BUG FIX #18d: OwnerPluginKey was missing.
            OwnerPluginKey = "WorkManagement.Activities",
            NavGroup = "Work Management",
            NavOrder = 30,
            DefaultListColumns = new[] { "Title", "ProjectId", "StartDate", "EndDate" },
            Fields = new[]
            {
                new FieldDefinition
                {
                    FieldName = "Title", DisplayName = "Title",
                    Type = FieldType.Text, IsRequired = true,
                    MaxLength = 300, SortOrder = 1, IsFilterable = true
                },
                new FieldDefinition
                {
                    FieldName = "Description", DisplayName = "Description",
                    Type = FieldType.RichText, IsRequired = false,
                    SortOrder = 2, ShowInList = false
                },
                new FieldDefinition
                {
                    FieldName = "ProjectId", DisplayName = "Project",
                    Type = FieldType.Relation, IsRequired = true,
                    SortOrder = 3, RelatedEntityType = "Project", IsFilterable = true
                },
                new FieldDefinition
                {
                    FieldName = "StartDate", DisplayName = "Start date",
                    Type = FieldType.DateTime, IsRequired = false,
                    SortOrder = 4, IsFilterable = true
                },
                new FieldDefinition
                {
                    FieldName = "EndDate", DisplayName = "End date",
                    Type = FieldType.DateTime, IsRequired = false,
                    SortOrder = 5, IsFilterable = true
                },
            },
            Actions = new[]
            {
                new ActionDefinition
                {
                    ActionKey = "create", DisplayName = "New activity", Icon = "plus",
                    HttpMethod = "POST", UrlTemplate = "/api/activities",
                    Placement = ActionPlacement.Toolbar
                },
                new ActionDefinition
                {
                    ActionKey = "edit", DisplayName = "Edit", Icon = "edit",
                    HttpMethod = "PUT", UrlTemplate = "/api/activities/{id}",
                    Placement = ActionPlacement.Row
                },
                new ActionDefinition
                {
                    ActionKey = "delete", DisplayName = "Delete", Icon = "trash",
                    HttpMethod = "DELETE", UrlTemplate = "/api/activities/{id}",
                    RequiredPermission = "Activities.Delete",
                    Placement = ActionPlacement.Row
                },
            }
        }
    };
}
