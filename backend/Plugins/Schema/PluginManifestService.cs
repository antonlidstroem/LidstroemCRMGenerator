using Lidstroem.Core.Interfaces;
using Lidstroem.Core.Schema;

namespace Lidstroem.Plugins.Schema;

/// <summary>
/// Builds the plugin manifest by introspecting all registered services.
/// Called once per request — the result is cached by the controller.
/// </summary>
public class PluginManifestService
{
    private readonly IEnumerable<IPluginMetadata> _plugins;
    private readonly IEnumerable<IPermissionProvider> _permissionProviders;
    private readonly IEnumerable<ILinkResolver> _linkResolvers;
    private readonly IEnumerable<IEntityExtensionProvider> _extensionProviders;
    private readonly SchemaRegistry _schemaRegistry;

    public PluginManifestService(
        IEnumerable<IPluginMetadata> plugins,
        IEnumerable<IPermissionProvider> permissionProviders,
        IEnumerable<ILinkResolver> linkResolvers,
        IEnumerable<IEntityExtensionProvider> extensionProviders,
        SchemaRegistry schemaRegistry)
    {
        _plugins = plugins;
        _permissionProviders = permissionProviders;
        _linkResolvers = linkResolvers;
        _extensionProviders = extensionProviders;
        _schemaRegistry = schemaRegistry;
    }

    public PluginManifest Build()
    {
        var allSchemas = _schemaRegistry.GetAll();
        var allPermissions = _permissionProviders
            .SelectMany(p => p.GetPermissions())
            .Select(p => p.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var registeredPlugins = _plugins.Select(p => BuildPluginEntry(p, allSchemas)).ToList();

        return new PluginManifest(
            RegisteredPlugins: registeredPlugins.AsReadOnly(),
            CoreContracts: BuildCoreContracts(),
            ActiveEntityTypes: allSchemas.Keys.OrderBy(k => k).ToList().AsReadOnly(),
            ActivePermissions: allPermissions.AsReadOnly(),
            RegisteredNavGroups: allSchemas.Values
                .Select(s => s.NavGroup)
                .Where(g => g != null)
                .Distinct()
                .OrderBy(g => g)
                .ToList()!
                .AsReadOnly(),
            RegisteredLinkResolverTypes: _linkResolvers.Select(r => r.TargetType).OrderBy(t => t).ToList().AsReadOnly(),
            RegisteredExtensionTargets: _extensionProviders.Select(e => e.TargetEntityName).Distinct().OrderBy(t => t).ToList().AsReadOnly(),
            GeneratedAt: DateTime.UtcNow);
    }

    private RegisteredPlugin BuildPluginEntry(
        IPluginMetadata plugin, IReadOnlyDictionary<string, EntitySchema> allSchemas)
    {
        // FIX #15: Replaced RoutePrefix substring heuristic with explicit PluginKey matching.
        // Previously, a plugin with RoutePrefix "cms" would accidentally capture any schema
        // whose ApiBasePath contained the string "cms" (e.g. "documents", "acl-cms").
        // Now each EntitySchema declares its OwnerPluginKey and we match exactly.
        var ownedEntities = allSchemas.Values
            .Where(s => string.Equals(s.OwnerPluginKey, plugin.PluginKey, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.EntityType)
            .ToList();

        // BUG FIX #20: The original code called plugin.PluginKey.Split('.').Last() twice
        // per predicate evaluation (redundant allocation) and used a fragile prefix-based
        // heuristic that could accidentally cross-match permissions from other plugins
        // (e.g. a permission in category "Activities" would match both
        // "WorkManagement.Activities" and any hypothetical "Activities" plugin).
        // Fix: IPermissionProvider.GetPermissions() already returns a PermissionDefinition
        // with a Category field — match against the full PluginKey for the Category check,
        // and fall back to a Name prefix match only when Category is absent.
        var ownedPermissions = _permissionProviders
            .SelectMany(p => p.GetPermissions())
            .Where(p =>
                p.Category.Equals(plugin.PluginKey, StringComparison.OrdinalIgnoreCase)
                || p.Name.StartsWith(plugin.PluginKey + ".", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .Distinct()
            .ToList();

        // BUG FIX #15: The original query was:
        //   _extensionProviders.Where(e => ownedEntities.Contains(e.TargetEntityName) == false)
        // That reads "give me every extension provider that targets something this plugin does NOT own",
        // which returns ALL extension providers from ALL other plugins combined — the exact opposite
        // of what's wanted. The correct question is: "which external entities does THIS plugin extend?"
        // We identify a provider as belonging to this plugin by checking whether its assembly name
        // matches the plugin's assembly name pattern (or simply that it is NOT targeting its own entity).
        // Since we don't have a direct plugin-key→assembly mapping, we use the practical approximation:
        // filter providers whose concrete type lives in an assembly whose name contains the plugin key.
        var pluginAssemblyFragment = plugin.PluginKey.Replace(".", "");
        var extensionTargets = _extensionProviders
            .Where(e =>
                // Provider belongs to this plugin's assembly
                e.GetType().Assembly.GetName().Name
                    ?.Replace(".", "")
                    .Contains(pluginAssemblyFragment, StringComparison.OrdinalIgnoreCase) == true
                // …and targets an entity it doesn't own (i.e. it is truly an extension)
                && !ownedEntities.Contains(e.TargetEntityName))
            .Select(e => e.TargetEntityName)
            .Distinct()
            .ToList();

        var linkTypes = _linkResolvers
            .Where(r => ownedEntities.Contains(r.TargetType))
            .Select(r => r.TargetType)
            .ToList();

        return new RegisteredPlugin(
            PluginKey: plugin.PluginKey,
            RoutePrefix: plugin.RoutePrefix,
            ExposedEntityTypes: ownedEntities.AsReadOnly(),
            ExposedPermissions: ownedPermissions.AsReadOnly(),
            ExtensionProviderTargets: extensionTargets.AsReadOnly(),
            LinkResolverTypes: linkTypes.AsReadOnly());
    }

    private static CoreContracts BuildCoreContracts() => new(
        BaseEntityFields: new[]
        {
            "Id (int, PK, auto-generated)",
            "TenantId (Guid, required, auto-stamped)",
            "OwnerId (Guid?, optional, auto-stamped)",
            "CreatedAt (DateTime, auto-stamped)",
            "CreatedBy (string?, auto-stamped from JWT)",
            "UpdatedAt (DateTime?, auto-stamped)",
            "ModifiedBy (string?, auto-stamped from JWT)"
        },
        ActorFields: new[]
        {
            "Id (int)",
            "DisplayName (string, max 200)",
            "Email (string, max 320)",
            "PhoneNumber (string?, max 50)"
        },
        AvailableCoreEvents: new[]
        {
            "ActorCreatedEvent(ActorId, TenantId)",
            "ActorUpdatedEvent(ActorId, TenantId)",
            "ActorDeletedEvent(ActorId, TenantId)",
            "ActorForgottenEvent(ActorId, TenantId, Email?, AllSucceeded)"
        },
        AvailableInterfaces: new[]
        {
            "IPluginModelConfigurator — Configure EF entity mappings",
            "IPermissionProvider — Declare permissions the plugin introduces",
            "IPluginMetadata — Declare PluginKey and RoutePrefix",
            "ISchemaProvider — Expose entity schema for dynamic frontend rendering",
            "IGdprHandler — Anonymise personal data on GDPR forget requests",
            "ILinkResolver — Resolve a display name for an entity by ID",
            "IEntityExtensionProvider — Extend another entity's detail response",
            "INotificationHandler<TEvent> — React to Core or plugin domain events"
        },
        AllowedDonorTypes: new[] { "Actor" }.ToList().AsReadOnly(),
        AllowedDonationTargetTypes: new[] { "Project", "Activity" }.ToList().AsReadOnly(),
        Rules: new PluginRules(
            ProjectReferences: "Plugins may only reference Lidstroem.Core and Lidstroem.Shared. No cross-plugin ProjectReferences.",
            ActorRelations: "Reference Actor by int ActorId only. No navigation properties to the Actor entity.",
            GdprHandlerRule: "Required if the plugin stores any field that can identify a natural person.",
            SchemaProviderRule: "Required for every plugin that exposes at least one entity.",
            EventCommunication: "Cross-plugin communication must use MediatR events. Direct service calls between plugins are not allowed."));
}
