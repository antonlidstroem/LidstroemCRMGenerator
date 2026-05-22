namespace Lidstroem.Plugins.Schema;

/// <summary>
/// The full manifest returned by GET /api/plugin-manifest.
/// An AI uses this to understand the current system state before
/// generating a new plugin.
/// </summary>
public record PluginManifest(
    IReadOnlyList<RegisteredPlugin> RegisteredPlugins,
    CoreContracts CoreContracts,
    IReadOnlyList<string> ActiveEntityTypes,
    IReadOnlyList<string> ActivePermissions,
    IReadOnlyList<string> RegisteredNavGroups,
    IReadOnlyList<string> RegisteredLinkResolverTypes,
    IReadOnlyList<string> RegisteredExtensionTargets,
    DateTime GeneratedAt);

public record RegisteredPlugin(
    string PluginKey,
    string RoutePrefix,
    IReadOnlyList<string> ExposedEntityTypes,
    IReadOnlyList<string> ExposedPermissions,
    IReadOnlyList<string> ExtensionProviderTargets,
    IReadOnlyList<string> LinkResolverTypes);

public record CoreContracts(
    IReadOnlyList<string> BaseEntityFields,
    IReadOnlyList<string> ActorFields,
    IReadOnlyList<string> AvailableCoreEvents,
    IReadOnlyList<string> AvailableInterfaces,
    IReadOnlyList<string> AllowedDonorTypes,
    IReadOnlyList<string> AllowedDonationTargetTypes,
    PluginRules Rules);

public record PluginRules(
    string ProjectReferences,
    string ActorRelations,
    string GdprHandlerRule,
    string SchemaProviderRule,
    string EventCommunication);
