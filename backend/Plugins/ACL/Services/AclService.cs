using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.ACL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lidstroem.Plugins.ACL.Services;

public class AclService : IAclService
{
    private readonly DbContext _context;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public AclService(DbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task GrantAsync(AclEntry entry)
    {
        var existing = await _context.Set<AclGrant>().FirstOrDefaultAsync(g =>
            g.GrantedToActorId == entry.GrantedToActorId
         && g.ResourceType == entry.ResourceType
         && g.ResourceId == entry.ResourceId
         && g.Action == entry.Action);

        if (existing != null)
        {
            existing.IsActive = true;
            existing.ExpiresAt = entry.ExpiresAt;
        }
        else
        {
            _context.Set<AclGrant>().Add(new AclGrant
            {
                ResourceId = entry.ResourceId,
                ResourceType = entry.ResourceType,
                GrantedByActorId = entry.GrantedByActorId,
                GrantedToActorId = entry.GrantedToActorId,
                Action = entry.Action,
                ExpiresAt = entry.ExpiresAt,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync();
        InvalidateCache(entry.GrantedToActorId, entry.ResourceId, entry.ResourceType);
    }

    public async Task RevokeAsync(int resourceId, string resourceType, int grantedToActorId, AclAction action)
    {
        var grant = await _context.Set<AclGrant>().FirstOrDefaultAsync(g =>
            g.ResourceId == resourceId
         && g.ResourceType == resourceType
         && g.GrantedToActorId == grantedToActorId
         && g.Action == action);

        if (grant == null) return;
        grant.IsActive = false;
        await _context.SaveChangesAsync();
        InvalidateCache(grantedToActorId, resourceId, resourceType);
    }

    public async Task<bool> HasAccessAsync(int actorId, int resourceId, string resourceType, AclAction action)
    {
        var key = CacheKey(actorId, resourceId, resourceType, action);
        if (_cache.TryGetValue(key, out bool cached)) return cached;

        var now = DateTime.UtcNow;
        var has = await _context.Set<AclGrant>().AnyAsync(g =>
            g.GrantedToActorId == actorId
         && g.ResourceId == resourceId
         && g.ResourceType == resourceType
         && g.Action == action
         && g.IsActive
         && (g.ExpiresAt == null || g.ExpiresAt > now));

        _cache.Set(key, has, CacheTtl);
        return has;
    }

    public async Task<IReadOnlyList<AclEntry>> GetGrantsAsync(int resourceId, string resourceType)
    {
        var grants = await _context.Set<AclGrant>()
            .Where(g => g.ResourceId == resourceId && g.ResourceType == resourceType && g.IsActive)
            .ToListAsync();

        return grants
            .Select(g => new AclEntry(g.ResourceId, g.ResourceType, g.GrantedByActorId, g.GrantedToActorId, g.Action, g.ExpiresAt))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<AclEntry>> GetActorGrantsAsync(int actorId)
    {
        var now = DateTime.UtcNow;
        var grants = await _context.Set<AclGrant>()
            .Where(g => g.GrantedToActorId == actorId && g.IsActive && (g.ExpiresAt == null || g.ExpiresAt > now))
            .ToListAsync();

        return grants
            .Select(g => new AclEntry(g.ResourceId, g.ResourceType, g.GrantedByActorId, g.GrantedToActorId, g.Action, g.ExpiresAt))
            .ToList()
            .AsReadOnly();
    }

    private void InvalidateCache(int actorId, int resourceId, string resourceType)
    {
        foreach (var action in Enum.GetValues<AclAction>())
            _cache.Remove(CacheKey(actorId, resourceId, resourceType, action));
    }

    // FIX #4: Check if the actor was the original granter on any active grant for this resource
    public async Task<bool> IsGranterAsync(int actorId, int resourceId, string resourceType) =>
        await _context.Set<AclGrant>().AnyAsync(g =>
            g.GrantedByActorId == actorId
         && g.ResourceId == resourceId
         && g.ResourceType == resourceType
         && g.IsActive);

    private static string CacheKey(int actorId, int resourceId, string resourceType, AclAction action) =>
        $"acl:{actorId}:{resourceType}:{resourceId}:{action}";
}
