using Lidstroem.Core.Schema;

namespace Lidstroem.Plugins.Donations;

public class DonationSchemaProvider : ISchemaProvider
{
    public IReadOnlyCollection<EntitySchema> GetSchemas() => new[]
    {
        new EntitySchema
        {
            EntityType = "Donation",
            DisplayName = "Donation",
            DisplayNamePlural = "Donations",
            Icon = "heart",
            ApiBasePath = "/api/donations",
            // BUG FIX #18b: OwnerPluginKey was missing — PluginManifestService could not
            // associate this schema with the Donations plugin, leaving ExposedEntityTypes empty.
            OwnerPluginKey = "Donations",
            NavGroup = "Finance",
            NavOrder = 50,
            DefaultListColumns = new[] { "Amount", "Currency", "DonorType", "DonationDate" },
            Fields = new[]
            {
                new FieldDefinition { FieldName = "Amount",       DisplayName = "Amount",      Type = FieldType.Decimal, IsRequired = true,  Min = 0,    SortOrder = 1 },
                new FieldDefinition { FieldName = "Currency",     DisplayName = "Currency",    Type = FieldType.Text,    IsRequired = true,  MaxLength = 3, SortOrder = 2 },
                new FieldDefinition { FieldName = "DonationDate", DisplayName = "Date",        Type = FieldType.DateTime,IsRequired = true,  SortOrder = 3, IsFilterable = true },
                new FieldDefinition { FieldName = "DonorId",      DisplayName = "Donor ID",    Type = FieldType.Number,  IsRequired = false, SortOrder = 4, ShowInList = false },
                new FieldDefinition { FieldName = "DonorType",    DisplayName = "Donor Type",  Type = FieldType.Text,    IsRequired = false, SortOrder = 5, IsFilterable = true },
                new FieldDefinition { FieldName = "TargetId",     DisplayName = "Target ID",   Type = FieldType.Number,  IsRequired = false, SortOrder = 6, ShowInList = false },
                new FieldDefinition { FieldName = "TargetType",   DisplayName = "Target Type", Type = FieldType.Text,    IsRequired = false, SortOrder = 7, IsFilterable = true },
            },
            Actions = new[]
            {
                new ActionDefinition { ActionKey = "create", DisplayName = "New donation", Icon = "plus",  HttpMethod = "POST",   UrlTemplate = "/api/donations",     Placement = ActionPlacement.Toolbar },
                new ActionDefinition { ActionKey = "edit",   DisplayName = "Edit",         Icon = "edit",  HttpMethod = "PUT",    UrlTemplate = "/api/donations/{id}", Placement = ActionPlacement.Row },
                new ActionDefinition { ActionKey = "delete", DisplayName = "Delete",       Icon = "trash", HttpMethod = "DELETE", UrlTemplate = "/api/donations/{id}", RequiredPermission = "Donations.Delete", Placement = ActionPlacement.Row },
            }
        }
    };
}
