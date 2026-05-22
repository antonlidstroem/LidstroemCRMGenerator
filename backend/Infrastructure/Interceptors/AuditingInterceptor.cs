using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lidstroem.Infrastructure.Interceptors;

public class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext _userContext;

    public AuditingInterceptor(ICurrentUserContext userContext)
    {
        _userContext = userContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context == null) return;

        var now = DateTime.UtcNow;
        var userId = _userContext.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                    if (entry.Entity.CreatedBy == null) entry.Entity.CreatedBy = userId;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.ModifiedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.ModifiedBy = userId;
                    break;
            }
        }
    }
}
