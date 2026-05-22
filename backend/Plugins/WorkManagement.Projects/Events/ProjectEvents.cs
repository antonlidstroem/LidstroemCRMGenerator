using Lidstroem.Core.Events;

namespace Lidstroem.Plugins.WorkManagement.Projects.Events;

public record ProjectCreatedEvent(int ProjectId, Guid TenantId) : IDomainEvent;
public record ProjectDeletedEvent(int ProjectId, Guid TenantId) : IDomainEvent;
