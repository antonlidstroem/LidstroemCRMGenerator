using Lidstroem.Core.GDPR;
using Lidstroem.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.GDPR;

/// <summary>
/// Anonymises the Actor record itself as part of a GDPR forget operation.
/// Registered automatically via IGdprHandler scanning.
/// </summary>
public class ActorGdprHandler : IGdprHandler
{
    private readonly DbContext _context;
    public string HandlerName => "Core.Actor";

    public ActorGdprHandler(DbContext context) => _context = context;

    public async Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId, string subjectType, Guid tenantId, CancellationToken ct = default)
    {
        if (!string.Equals(subjectType, "Actor", StringComparison.OrdinalIgnoreCase))
            return GdprHandlerResult.Skipped(HandlerName);

        try
        {
            var actor = await _context.Set<Actor>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == subjectId && a.TenantId == tenantId, ct);

            if (actor == null) return GdprHandlerResult.Ok(HandlerName, 0);

            actor.DisplayName = "[deleted]";
            actor.Email = $"forgotten-{subjectId}@gdpr.invalid";
            actor.PhoneNumber = null;

            await _context.SaveChangesAsync(ct);
            return GdprHandlerResult.Ok(HandlerName, 1);
        }
        catch (Exception ex)
        {
            return GdprHandlerResult.Failed(HandlerName, ex.Message);
        }
    }
}
