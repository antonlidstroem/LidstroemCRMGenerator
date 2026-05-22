using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.SuperAdmin.Entities;

public class Tenant : BaseEntity
{
    public Guid ExternalId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ActivatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public int? ActorQuota { get; set; }
    public List<TenantPluginAssignment> PluginAssignments { get; set; } = new();
    public List<TenantCustomPage> CustomPages { get; set; } = new();
}

public class TenantPluginAssignment : BaseEntity
{
    public int TenantEntityId { get; set; }
    public Tenant? TenantEntity { get; set; }
    public string PluginKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime? EnabledAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}

/// <summary>
/// Records which custom pages are active for a tenant.
/// One row per page per tenant — toggled by SuperAdmin.
/// </summary>
public class TenantCustomPage : BaseEntity
{
    public int TenantEntityId { get; set; }
    public Tenant? TenantEntity { get; set; }

    /// <summary>Matches ICustomPageMetadata.PageKey</summary>
    public string PageKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
    public DateTime? EnabledAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}

public class SystemLog : BaseEntity
{
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public string? RequestPath { get; set; }
    public string? RequestId { get; set; }
}
