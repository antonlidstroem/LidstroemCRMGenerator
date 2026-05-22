using Lidstroem.Core.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lidstroem.Plugins.Schema.Controllers;

[Route("api/schema")]
[ApiController]
[AllowAnonymous]
public class SchemaController : ControllerBase
{
    private readonly SchemaRegistry _registry;

    public SchemaController(SchemaRegistry registry) => _registry = registry;

    [HttpGet]
    public ActionResult<IReadOnlyDictionary<string, EntitySchema>> GetAll() =>
        Ok(_registry.GetAll());

    [HttpGet("{entityType}")]
    public ActionResult<EntitySchema> Get(string entityType)
    {
        var schema = _registry.Get(entityType);
        return schema == null ? NotFound($"No schema for '{entityType}'.") : Ok(schema);
    }

    [HttpGet("types")]
    public ActionResult<IReadOnlyCollection<string>> GetTypes() =>
        Ok(_registry.GetRegisteredTypes());
}
