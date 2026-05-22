using Lidstroem.Core.Constants;
using Lidstroem.Core.Interfaces;

namespace Lidstroem.Infrastructure.Services;

public class SystemTenantContext : ITenantContext
{
    public Guid TenantId => TenantConstants.SystemTenantId;
    public Guid? OwnerId => null;
    public bool IsSystemContext => true;
}

public class SystemCurrentUserContext : ICurrentUserContext
{
    public string? UserId => "system";
    public string? DisplayName => "System";
}
