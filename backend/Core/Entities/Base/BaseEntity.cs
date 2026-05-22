using System.ComponentModel.DataAnnotations.Schema;
using Lidstroem.Core.Events;

namespace Lidstroem.Core.Entities.Base;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ModifiedBy { get; set; }

    [NotMapped]
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
