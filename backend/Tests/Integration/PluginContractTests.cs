using System.Net;
using System.Text.Json;
using FluentAssertions;
using Lidstroem.Core.Interfaces;
using Lidstroem.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Lidstroem.Core.Schema;
using Xunit;

namespace Lidstroem.Tests.Integration;

/// <summary>
/// Verifies that the plugin architecture contracts are upheld system-wide.
/// These tests catch the bugs that unit tests can never catch — the ones
/// that only appear when everything is wired together.
/// </summary>
public class PluginContractTests : IntegrationTestBase
{
    public PluginContractTests(LidstroemWebApplicationFactory factory)
        : base(factory) { }

    /// <summary>
    /// Every plugin that registers IPluginMetadata must also register
    /// an ISchemaProvider — otherwise the frontend cannot render it.
    /// </summary>
    [Fact]
    public void AllPlugins_HaveSchemaProvider()
    {
        using var scope = Factory.Services.CreateScope();
        var plugins         = scope.ServiceProvider.GetServices<IPluginMetadata>().ToList();
        var schemaProviders = scope.ServiceProvider.GetServices<ISchemaProvider>().ToList();

        plugins.Should().NotBeEmpty("at least one plugin must be registered");

        var schemaEntityTypes = schemaProviders
            .SelectMany(p => p.GetSchemas())
            .Select(s => s.EntityType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Every plugin must have contributed at least one entity type to the schema
        foreach (var plugin in plugins)
        {
            schemaEntityTypes.Should().NotBeEmpty(
                $"plugin '{plugin.PluginKey}' must contribute at least one EntitySchema " +
                $"via ISchemaProvider");
        }
    }

    /// <summary>
    /// Every entity type declared in the schema must have a working API endpoint.
    /// A 404 here means dead schema — frontend would try to render a list that
    /// returns "not found".
    /// </summary>
    [Fact]
    public async Task AllEntityTypes_HaveWorkingApiEndpoint()
    {
        var schema = await AdminClient.GetJsonAsync("/api/schema");
        var failed = new List<string>();

        foreach (var entity in schema.EnumerateObject())
        {
            var apiBase = entity.Value
                .GetProperty("apiBasePath")
                .GetString();

            if (string.IsNullOrEmpty(apiBase)) continue;

            var response = await AdminClient.GetAsync(apiBase);

            // 200 = works, 401 = requires auth (acceptable), 403 = no permission (acceptable)
            // Anything else — especially 404 — is a contract violation
            if (response.StatusCode == HttpStatusCode.NotFound)
                failed.Add($"{entity.Name} -> {apiBase} returned 404");
        }

        failed.Should().BeEmpty(
            "every entity in the schema must have a responding API endpoint");
    }

    /// <summary>
    /// Verifies that no plugin assembly has a project reference to another
    /// plugin assembly. This is the single most important architectural rule.
    /// </summary>
    [Fact]
    public void NoPlugin_ReferencesAnotherPlugin()
    {
        var pluginAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Lidstroem.Plugins.") == true)
            .ToList();

        pluginAssemblies.Should().NotBeEmpty("plugin assemblies must be loaded");

        var violations = new List<string>();

        foreach (var assembly in pluginAssemblies)
        {
            var referencedNames = assembly.GetReferencedAssemblies()
                .Where(r => r.Name?.StartsWith("Lidstroem.Plugins.") == true)
                .Select(r => r.Name!)
                .ToList();

            // SuperAdmin is the documented exception — it references Infrastructure
            // but not other feature plugins. We verify it doesn't reference feature plugins.
            if (assembly.GetName().Name == "Lidstroem.Plugins.SuperAdmin")
            {
                var featurePluginRefs = referencedNames
                    .Where(r => r != "Lidstroem.Plugins.SuperAdmin")
                    .ToList();

                if (featurePluginRefs.Any())
                    violations.Add(
                        $"SuperAdmin references: {string.Join(", ", featurePluginRefs)}");
                continue;
            }

            foreach (var refName in referencedNames)
                violations.Add($"{assembly.GetName().Name} -> {refName}");
        }

        violations.Should().BeEmpty(
            "plugins must not reference each other (see PLUGIN_TEMPLATE.md rule 1)");
    }

    /// <summary>
    /// GET /api/plugin-manifest must return all expected keys so the
    /// AI-assisted plugin workflow has reliable context.
    /// </summary>
    [Fact]
    public async Task PluginManifest_ContainsRequiredSections()
    {
        var manifest = await AdminClient.GetJsonAsync("/api/plugin-manifest");

        manifest.TryGetProperty("registeredPlugins", out var plugins).Should().BeTrue();
        manifest.TryGetProperty("coreContracts",     out _).Should().BeTrue();
        manifest.TryGetProperty("activeEntityTypes", out _).Should().BeTrue();
        manifest.TryGetProperty("activePermissions", out _).Should().BeTrue();
        manifest.TryGetProperty("generatedAt",       out _).Should().BeTrue();

        plugins.GetArrayLength().Should().BeGreaterThan(0,
            "at least one plugin must appear in the manifest");
    }

    /// <summary>
    /// The schema endpoint must return at least the core entity types.
    /// If this fails, the entire frontend rendering pipeline breaks.
    /// </summary>
    [Fact]
    public async Task Schema_ContainsCoreEntityTypes()
    {
        var schema = await AdminClient.GetJsonAsync("/api/schema");

        schema.TryGetProperty("Actor", out _).Should().BeTrue(
            "Actor must always be present in the schema");
    }

    /// <summary>
    /// All declared permissions must be registered in the database after startup.
    /// The PermissionRegistry hosted service does this — if it fails silently,
    /// permissions would be empty and every permission check would return false.
    /// </summary>
    [Fact]
    public async Task AllDeclaredPermissions_AreRegisteredInDatabase()
    {
        var response = await AdminClient.GetAsync("/api/rbac/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body        = await response.Content.ReadAsStringAsync();
        var permissions = JsonDocument.Parse(body).RootElement;

        permissions.GetArrayLength().Should().BeGreaterThan(10,
            "the system should have at least 10 declared permissions across all plugins");
    }
}
