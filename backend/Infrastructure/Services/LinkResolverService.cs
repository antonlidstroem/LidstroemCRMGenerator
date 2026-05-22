using Lidstroem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Infrastructure.Services;

public class LinkResolverService : ILinkResolverService
{
    private readonly IReadOnlyDictionary<string, ILinkResolver> _resolvers;
    private readonly DbContext _context;

    public LinkResolverService(IEnumerable<ILinkResolver> resolvers, DbContext context)
    {
        _context = context;
        _resolvers = resolvers.ToDictionary(
            r => r.TargetType, r => r,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> RegisteredTypes =>
        _resolvers.Keys.ToList().AsReadOnly();

    public async Task<string?> ResolveAsync(int targetId, string targetType)
    {
        if (!_resolvers.TryGetValue(targetType, out var resolver)) return null;
        return await resolver.ResolveNameAsync(targetId, _context);
    }
}
