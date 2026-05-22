using FluentAssertions;
using Lidstroem.Core.Entities.Base;
using Lidstroem.Core.Events;
using Xunit;

namespace Lidstroem.Tests.Core;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity { }
    private record TestEvent : IDomainEvent { }

    [Fact]
    public void AddDomainEvent_ShouldAppendToCollection()
    {
        var entity = new TestEntity();
        var @event = new TestEvent();

        entity.AddDomainEvent(@event);

        entity.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(@event);
    }

    [Fact]
    public void AddDomainEvent_Multiple_ShouldPreserveOrder()
    {
        var entity = new TestEntity();
        var first  = new TestEvent();
        var second = new TestEvent();

        entity.AddDomainEvent(first);
        entity.AddDomainEvent(second);

        entity.DomainEvents.Should().HaveCount(2);
        entity.DomainEvents.First().Should().Be(first);
        entity.DomainEvents.Last().Should().Be(second);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAll()
    {
        var entity = new TestEntity();
        entity.AddDomainEvent(new TestEvent());
        entity.AddDomainEvent(new TestEvent());

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_IsReadOnly_CannotBeModifiedExternally()
    {
        var entity = new TestEntity();

        // IReadOnlyCollection means we can't call Add/Remove on the returned reference
        var events = entity.DomainEvents;
        var action = () => ((List<IDomainEvent>)events).Add(new TestEvent());

        action.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var entity = new TestEntity();
        var after  = DateTime.UtcNow.AddSeconds(1);

        entity.CreatedAt.Should().BeAfter(before).And.BeBefore(after);
    }
}
