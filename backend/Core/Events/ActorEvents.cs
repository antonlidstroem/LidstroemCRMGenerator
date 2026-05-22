using Lidstroem.Core.Events;

namespace Lidstroem.Core.Events;

public record ActorCreatedEvent(int ActorId, Guid TenantId) : IDomainEvent;
public record ActorUpdatedEvent(int ActorId, Guid TenantId) : IDomainEvent;
public record ActorDeletedEvent(int ActorId, Guid TenantId) : IDomainEvent;

/// <summary>
/// Published after a successful GDPR forget operation.
/// Email is the pre-anonymisation address, used for confirmation sending.
/// </summary>
public record ActorForgottenEvent(
    int ActorId,
    Guid TenantId,
    string? Email,
    bool AllSucceeded) : IDomainEvent;
