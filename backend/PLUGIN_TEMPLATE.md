# Lidstroem Plugin Template

This document is the authoritative guide for building a new plugin.
Feed this file + the output of `GET /api/plugin-manifest` to an AI to generate
all required files for a new plugin.

---

## Invariant rules — never break these

1. **No cross-plugin ProjectReferences.**
   A plugin's `.csproj` may only reference `Lidstroem.Core` and `Lidstroem.Shared`.
   The SuperAdmin plugin is the sole exception (it references Infrastructure for seeding).

2. **No navigation properties to Actor or any other plugin entity.**
   Reference Actor by `int ActorId` only. For loose polymorphic relations, use
   `int? TargetId` + `string? TargetType` and validate TargetType at the API layer.

3. **IGdprHandler is required** if the plugin stores any field that can identify a person.

4. **ISchemaProvider is required** for every plugin that exposes at least one entity.

5. **Cross-plugin communication via MediatR only.**
   Listen to Core events (`ActorCreatedEvent`, etc.) via `INotificationHandler<TEvent>`.
   Never call another plugin's service directly.

6. **All endpoints use `[RequirePermission("X.Y")]`.**
   Declare every permission in `IPermissionProvider`.

---

## File structure — exactly these files, no more

```
Lidstroem.Plugins.{Name}/
├── Lidstroem.Plugins.{Name}.csproj
├── plugin-manifest.json
├── Entities/
│   └── {Entity}.cs                  (one file per entity)
├── DTOs/
│   └── {Entity}Dto.cs               (one file per entity)
├── Controllers/
│   └── {Entity}Controller.cs        (one file per entity)
└── {Name}Support.cs                 (all support types in one file)
```

---

## 1. Project file — `Lidstroem.Plugins.{Name}.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="MediatR" Version="12.4.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.0" />
    <!-- Add extra NuGet packages here if needed -->
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Lidstroem.Core\Lidstroem.Core.csproj" />
    <ProjectReference Include="..\..\Lidstroem.Shared\Lidstroem.Shared.csproj" />
  </ItemGroup>
</Project>
```

---

## 2. Plugin manifest — `plugin-manifest.json`

```json
{
  "pluginKey": "{Name}",
  "displayName": "{Human-readable name}",
  "description": "{What the plugin does}",
  "version": "1.0.0",
  "routePrefix": "{lowercase-plural}",
  "entities": [
    {
      "name": "{Entity}",
      "hasGdprHandler": true,
      "hasLinkResolver": true,
      "hasExtensionProvider": [],
      "polymorphicRelations": [],
      "fields": []
    }
  ],
  "permissions": [],
  "listensToEvents": [],
  "publishesEvents": [],
  "navGroup": "{Group name or null for top-level}",
  "navOrder": 100,
  "dependsOn": [],
  "optionallyExtends": []
}
```

---

## 3. Entity — `Entities/{Entity}.cs`

```csharp
using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.{Name}.Entities;

public class {Entity} : BaseEntity
{
    // Fields here.
    // Rules:
    //   - No navigation properties to other plugin entities
    //   - Reference Actor by int ActorId only (never Actor? Actor)
    //   - For polymorphic targets: int? TargetId + string? TargetType
    //   - Validate TargetType values in the DTO, not here
    public string Title { get; set; } = string.Empty;
}
```

**Join entities** (if needed for many-to-many with Actor):
```csharp
public class {Entity}Actor
{
    public int {Entity}Id { get; set; }
    public {Entity}? {Entity} { get; set; }
    public int ActorId { get; set; }
    // No navigation to Actor
}
```

---

## 4. DTO — `DTOs/{Entity}Dto.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Lidstroem.Plugins.{Name}.DTOs;

public class {Entity}Dto : IValidatableObject
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    // If polymorphic target:
    public int? TargetId { get; set; }
    public string? TargetType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        // Validate TargetType against allowed set here
        yield break;
    }
}
```

---

## 5. Support file — `{Name}Support.cs`

Put all support types in one file to keep the structure minimal.

```csharp
using Lidstroem.Core.GDPR;
using Lidstroem.Core.Interfaces;
using Lidstroem.Core.Schema;
using Lidstroem.Plugins.{Name}.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.{Name};

// ── EF configuration ─────────────────────────────────────────────────────────

public class {Name}ModelConfigurator : IPluginModelConfigurator
{
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<{Entity}>(entity =>
        {
            entity.ToTable("{Entity}");
            // Add indexes here
            entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_{Entity}_TenantId");
        });
    }
}

// ── Permissions ───────────────────────────────────────────────────────────────

public class {Name}PermissionProvider : IPermissionProvider
{
    public IReadOnlyCollection<PermissionDefinition> GetPermissions() => new[]
    {
        new PermissionDefinition("{Name}.View",   "View {entities}",   "List and read {entities}", "{Category}"),
        new PermissionDefinition("{Name}.Create", "Create {entity}",   "Create new {entity}",      "{Category}"),
        new PermissionDefinition("{Name}.Edit",   "Edit {entity}",     "Update {entity}",          "{Category}"),
        new PermissionDefinition("{Name}.Delete", "Delete {entity}",   "Remove {entity}",          "{Category}"),
    };
}

// ── Plugin metadata ───────────────────────────────────────────────────────────

public class {Name}PluginMetadata : IPluginMetadata
{
    public string PluginKey   => "{Name}";
    public string RoutePrefix => "{routeprefix}";
}

// ── Schema provider ───────────────────────────────────────────────────────────

public class {Name}SchemaProvider : ISchemaProvider
{
    public IReadOnlyCollection<EntitySchema> GetSchemas() => new[]
    {
        new EntitySchema
        {
            EntityType         = "{Entity}",
            DisplayName        = "{Entity}",
            DisplayNamePlural  = "{Entities}",
            Icon               = "{lucide-icon-name}",
            ApiBasePath        = "/api/{routeprefix}",
            NavGroup           = "{NavGroup or null}",
            NavOrder           = 100,
            DefaultListColumns = new[] { "Title" },
            Fields = new[]
            {
                new FieldDefinition
                {
                    FieldName  = "Title",
                    DisplayName = "Title",
                    Type       = FieldType.Text,
                    IsRequired = true,
                    MaxLength  = 300,
                    SortOrder  = 1,
                    IsFilterable = true
                },
            },
            Actions = new[]
            {
                new ActionDefinition { ActionKey = "create", DisplayName = "New {entity}", Icon = "plus",  HttpMethod = "POST",   UrlTemplate = "/api/{routeprefix}",     Placement = ActionPlacement.Toolbar },
                new ActionDefinition { ActionKey = "edit",   DisplayName = "Edit",         Icon = "edit",  HttpMethod = "PUT",    UrlTemplate = "/api/{routeprefix}/{id}", Placement = ActionPlacement.Row },
                new ActionDefinition { ActionKey = "delete", DisplayName = "Delete",       Icon = "trash", HttpMethod = "DELETE", UrlTemplate = "/api/{routeprefix}/{id}", RequiredPermission = "{Name}.Delete", Placement = ActionPlacement.Row },
            }
        }
    };
}

// ── GDPR handler (include if the entity stores personal data) ─────────────────

public class {Name}GdprHandler : IGdprHandler
{
    private readonly DbContext _context;
    public string HandlerName => "{Name}";

    public {Name}GdprHandler(DbContext context) => _context = context;

    public async Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId, string subjectType, Guid tenantId, CancellationToken ct = default)
    {
        if (!string.Equals(subjectType, "Actor", StringComparison.OrdinalIgnoreCase))
            return GdprHandlerResult.Skipped(HandlerName);

        try
        {
            // Anonymise or delete records linked to this actor
            var records = await _context.Set<{Entity}>().IgnoreQueryFilters()
                .Where(e => e.ActorId == subjectId && e.TenantId == tenantId)
                .ToListAsync(ct);

            // Choose one:
            // Option A — anonymise (keep record, clear personal fields)
            foreach (var r in records) r.Title = "[deleted]";

            // Option B — hard delete
            // _context.Set<{Entity}>().RemoveRange(records);

            await _context.SaveChangesAsync(ct);
            return GdprHandlerResult.Ok(HandlerName, records.Count);
        }
        catch (Exception ex)
        {
            return GdprHandlerResult.Failed(HandlerName, ex.Message);
        }
    }
}

// ── Link resolver (include if other plugins need to look up this entity by ID) ─

public class {Entity}LinkResolver : ILinkResolver
{
    public string TargetType => "{Entity}";

    public async Task<string?> ResolveNameAsync(int targetId, DbContext context)
    {
        var entity = await context.Set<{Entity}>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == targetId);
        return entity?.Title;
    }
}

// ── Extension provider (include once per entity this plugin extends) ──────────

public class {TargetEntity}{Name}ExtensionProvider : IEntityExtensionProvider
{
    public string TargetEntityName => "{TargetEntity}";

    public async Task<object?> GetExtensionDataAsync(int entityId, DbContext context) =>
        await context.Set<{Entity}>()
            .Where(e => e.ActorId == entityId)   // adjust filter to match your FK
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Title, e.CreatedAt })
            .ToListAsync();
}

// ── Event handlers (include one per Core event you react to) ──────────────────

// Example: react when an Actor is deleted
// public class ActorDeletedHandler : INotificationHandler<ActorDeletedEvent>
// {
//     public async Task Handle(ActorDeletedEvent notification, CancellationToken ct)
//     {
//         // Clean up data linked to notification.ActorId
//     }
// }
```

---

## 6. Controller — `Controllers/{Entity}Controller.cs`

```csharp
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.{Name}.DTOs;
using Lidstroem.Plugins.{Name}.Entities;
using Lidstroem.Shared.Attributes;
using Lidstroem.Shared.Controllers.Base;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.{Name}.Controllers;

[Route("api/{routeprefix}")]
[ApiController]
[Authorize]
public class {Entity}Controller : BaseLidstroemController<{Entity}>
{
    public {Entity}Controller(
        DbContext context,
        IEnumerable<IEntityExtensionProvider> extenders,
        IPublisher publisher)
        : base(context, extenders, publisher) { }

    protected override Task MapDtoToEntity(object dto, {Entity} entity)
    {
        var d = ({Entity}Dto)dto;
        entity.Title = d.Title;
        // map other fields
        return Task.CompletedTask;
    }

    [HttpGet]
    [RequirePermission("{Name}.View")]
    public async Task<ActionResult<IEnumerable<{Entity}>>> GetAll() =>
        Ok(await _context.Set<{Entity}>().ToListAsync());

    [HttpGet("{id:int}")]
    [RequirePermission("{Name}.View")]
    public async Task<ActionResult<object>> GetOne(int id)
    {
        var entity = await _context.Set<{Entity}>().FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(await OkWithExtensions(entity, id));
    }

    [HttpPost]
    [RequirePermission("{Name}.Create")]
    public async Task<ActionResult<{Entity}>> Post({Entity}Dto dto) =>
        await PostGeneric(dto);

    [HttpPut("{id:int}")]
    [RequirePermission("{Name}.Edit")]
    public async Task<IActionResult> Put(int id, {Entity}Dto dto)
    {
        if (id != dto.Id) return BadRequest();
        return await PutGeneric(id, dto);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("{Name}.Delete")]
    public async Task<IActionResult> Delete(int id) =>
        await DeleteGeneric(id);
}
```

---

## 7. Add to WebAPI.csproj

```xml
<ProjectReference Include="..\Plugins\{Name}\Lidstroem.Plugins.{Name}.csproj" />
```

---

## 8. Add to create-sln.sh

```bash
dotnet sln add Plugins/{Name}/Lidstroem.Plugins.{Name}.csproj
```

---

## 9. Run migration

```bash
cd Infrastructure
dotnet ef migrations add Add{Name}Plugin --startup-project ../WebAPI
dotnet ef database update --startup-project ../WebAPI
```

No changes to `Program.cs` are needed — the system auto-discovers:
- `IPluginModelConfigurator` via `AppDbContext.OnModelCreating`
- `IPermissionProvider` via Scrutor scan in `InfrastructureExtensions`
- `IEntityExtensionProvider` via Scrutor scan
- `ILinkResolver` via Scrutor scan
- `IPluginMetadata` via Scrutor scan
- `IGdprHandler` via Scrutor scan in `GdprExtensions`
- `ISchemaProvider` via Scrutor scan in `SchemaExtensions`
- MediatR handlers via assembly scan
- Controllers via `AddApplicationPart`

---

## Checklist before submitting

```
□ .csproj references only Core and Shared
□ No navigation properties to Actor or other plugin entities
□ All ActorId references are int fields
□ Polymorphic TargetType validated against an explicit allowed set in DTO
□ IPluginModelConfigurator: table name + at least one index
□ IPermissionProvider: View/Create/Edit/Delete for each entity
□ IPluginMetadata: PluginKey + RoutePrefix
□ ISchemaProvider: EntitySchema with NavGroup + NavOrder
□ IGdprHandler: present if any field can identify a person
□ ILinkResolver: present if other plugins may need to resolve this entity's name
□ IEntityExtensionProvider: one per entity this plugin extends
□ Controller: RequirePermission on every endpoint
□ DTO: validation attributes + IValidatableObject for polymorphic type checks
□ plugin-manifest.json: filled in correctly
□ WebAPI.csproj: new ProjectReference added
□ create-sln.sh: new sln add line added
□ Migration run and verified
```
