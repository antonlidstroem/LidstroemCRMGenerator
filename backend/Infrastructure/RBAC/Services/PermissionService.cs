using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.RBAC.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lidstroem.Infrastructure.RBAC.Services;

public class PermissionService : IPermissionService
{
    private readonly DbContext _context;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public PermissionService(DbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<bool> HasPermissionAsync(int actorId, Guid tenantId, string permission)
    {
        var permissions = await GetPermissionsAsync(actorId, tenantId);
        return permissions.Contains(permission);
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(int actorId, Guid tenantId)
    {
        var key = CacheKey(actorId, tenantId);
        if (_cache.TryGetValue(key, out IReadOnlyCollection<string>? cached) && cached != null)
            return cached;

        var permissions = await _context.Set<ActorRoleAssignment>()
            .IgnoreQueryFilters()
            .Where(a => a.ActorId == actorId && a.TenantId == tenantId)
            .Include(a => a.Role)
                .ThenInclude(r => r!.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .SelectMany(a => a.Role!.RolePermissions
                .Where(rp => rp.Permission!.IsActive)
                .Select(rp => rp.Permission!.Name))
            .Distinct()
            .ToListAsync();

        var result = (IReadOnlyCollection<string>)permissions.AsReadOnly();
        _cache.Set(key, result, CacheTtl);
        return result;
    }

    public async Task AssignRoleAsync(int actorId, Guid tenantId, string roleName)
    {
        var role = await _context.Set<Role>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == roleName && r.TenantId == tenantId)
            ?? throw new InvalidOperationException(
                $"Role '{roleName}' not found in tenant {tenantId}.");

        var alreadyAssigned = await _context.Set<ActorRoleAssignment>()
            .IgnoreQueryFilters()
            .AnyAsync(a => a.ActorId == actorId && a.TenantId == tenantId && a.RoleId == role.Id);

        if (alreadyAssigned) return;

        _context.Set<ActorRoleAssignment>().Add(new ActorRoleAssignment
        {
            ActorId = actorId,
            RoleId = role.Id,
            TenantId = tenantId
        });

        await _context.SaveChangesAsync();
        InvalidateCache(actorId, tenantId);
    }

    public async Task RevokeRoleAsync(int actorId, Guid tenantId, string roleName)
    {
        var role = await _context.Set<Role>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == roleName && r.TenantId == tenantId);

        if (role == null) return;

        var assignment = await _context.Set<ActorRoleAssignment>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.ActorId == actorId
                                   && a.TenantId == tenantId
                                   && a.RoleId == role.Id);

        if (assignment == null) return;

        _context.Set<ActorRoleAssignment>().Remove(assignment);
        await _context.SaveChangesAsync();
        InvalidateCache(actorId, tenantId);
    }

    public void InvalidateCache(int actorId, Guid tenantId) =>
        _cache.Remove(CacheKey(actorId, tenantId));

    private static string CacheKey(int actorId, Guid tenantId) =>
        $"rbac:{tenantId}:{actorId}";
}
