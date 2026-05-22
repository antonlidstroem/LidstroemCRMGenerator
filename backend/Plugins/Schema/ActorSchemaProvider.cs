using Lidstroem.Core.Schema;

namespace Lidstroem.Plugins.Schema;

public class ActorSchemaProvider : ISchemaProvider
{
    public IReadOnlyCollection<EntitySchema> GetSchemas() => new[]
    {
        new EntitySchema
        {
            EntityType = "Actor",
            DisplayName = "Actor",
            DisplayNamePlural = "Actors",
            Icon = "user",
            ApiBasePath = "/api/actors",
            // BUG FIX #18a: OwnerPluginKey was missing. PluginManifestService.BuildPluginEntry
            // uses this to map schemas to their owning plugin. Without it, every plugin shows
            // an empty ExposedEntityTypes list in the manifest, breaking the AI plugin workflow.
            OwnerPluginKey = "SuperAdmin",
            NavGroup = null,
            NavOrder = 10,
            DefaultListColumns = new[] { "DisplayName", "Email" },
            Fields = new[]
            {
                new FieldDefinition
                {
                    FieldName = "DisplayName", DisplayName = "Name",
                    Type = FieldType.Text, IsRequired = true,
                    MaxLength = 200, SortOrder = 1, IsFilterable = true
                },
                new FieldDefinition
                {
                    FieldName = "Email", DisplayName = "Email",
                    Type = FieldType.Text, IsRequired = true,
                    MaxLength = 320, SortOrder = 2, IsFilterable = true,
                    UiHint = "email"
                },
                new FieldDefinition
                {
                    FieldName = "PhoneNumber", DisplayName = "Phone",
                    Type = FieldType.Text, IsRequired = false,
                    SortOrder = 3, ShowInList = false, UiHint = "phone"
                },
            },
            Actions = new[]
            {
                new ActionDefinition
                {
                    ActionKey = "create", DisplayName = "New actor", Icon = "plus",
                    HttpMethod = "POST", UrlTemplate = "/api/actors",
                    Placement = ActionPlacement.Toolbar
                },
                new ActionDefinition
                {
                    ActionKey = "edit", DisplayName = "Edit", Icon = "edit",
                    HttpMethod = "PUT", UrlTemplate = "/api/actors/{id}",
                    Placement = ActionPlacement.Row
                },
                new ActionDefinition
                {
                    ActionKey = "delete", DisplayName = "Delete", Icon = "trash",
                    HttpMethod = "DELETE", UrlTemplate = "/api/actors/{id}",
                    RequiredPermission = "Actors.Delete", Placement = ActionPlacement.Row
                },
                new ActionDefinition
                {
                    ActionKey = "forget", DisplayName = "GDPR Forget", Icon = "shield-off",
                    HttpMethod = "POST", UrlTemplate = "/api/gdpr/forget/{id}?subjectType=Actor",
                    RequiredPermission = "GDPR.Forget", Placement = ActionPlacement.Row
                },
            }
        }
    };
}
