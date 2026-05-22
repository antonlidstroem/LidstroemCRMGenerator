using Lidstroem.Core.Schema;

namespace Lidstroem.Plugins.FieldReports;

public class FieldReportSchemaProvider : ISchemaProvider
{
    public IReadOnlyCollection<EntitySchema> GetSchemas() => new[]
    {
        new EntitySchema
        {
            EntityType = "FieldReport",
            DisplayName = "Field Report",
            DisplayNamePlural = "Field Reports",
            Icon = "file-text",
            ApiBasePath = "/api/fieldreports",
            // BUG FIX #18c: OwnerPluginKey was missing.
            OwnerPluginKey = "FieldReports",
            NavGroup = "Work Management",
            NavOrder = 40,
            DefaultListColumns = new[] { "Title", "AuthorActorId", "CreatedAt" },
            Fields = new[]
            {
                new FieldDefinition { FieldName = "Title",         DisplayName = "Title",       Type = FieldType.Text,     IsRequired = true,  MaxLength = 300, SortOrder = 1, IsFilterable = true },
                new FieldDefinition { FieldName = "Content",       DisplayName = "Content",     Type = FieldType.RichText, IsRequired = true,  SortOrder = 2,  ShowInList = false },
                new FieldDefinition { FieldName = "AuthorActorId", DisplayName = "Author",      Type = FieldType.Relation, IsRequired = true,  SortOrder = 3,  RelatedEntityType = "Actor" },
                new FieldDefinition { FieldName = "ActivityId",    DisplayName = "Activity",    Type = FieldType.Relation, IsRequired = false, SortOrder = 4,  ShowInList = false, RelatedEntityType = "Activity" },
                new FieldDefinition { FieldName = "ActivityType",  DisplayName = "Activity Type",Type = FieldType.Text,   IsRequired = false, SortOrder = 5,  ShowInList = false },
                new FieldDefinition { FieldName = "ContextId",     DisplayName = "Context",     Type = FieldType.Relation, IsRequired = false, SortOrder = 6,  ShowInList = false, RelatedEntityType = "Project" },
                new FieldDefinition { FieldName = "ContextType",   DisplayName = "Context Type",Type = FieldType.Text,    IsRequired = false, SortOrder = 7,  ShowInList = false },
            },
            Actions = new[]
            {
                new ActionDefinition { ActionKey = "create", DisplayName = "New report", Icon = "plus",  HttpMethod = "POST",   UrlTemplate = "/api/fieldreports",     Placement = ActionPlacement.Toolbar },
                new ActionDefinition { ActionKey = "edit",   DisplayName = "Edit",       Icon = "edit",  HttpMethod = "PUT",    UrlTemplate = "/api/fieldreports/{id}", Placement = ActionPlacement.Row },
                new ActionDefinition { ActionKey = "delete", DisplayName = "Delete",     Icon = "trash", HttpMethod = "DELETE", UrlTemplate = "/api/fieldreports/{id}", RequiredPermission = "FieldReports.Delete", Placement = ActionPlacement.Row },
            }
        }
    };
}
