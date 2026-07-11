using GarageFlow.Application.Common.Events;
using GarageFlow.SharedKernel.Domain.Events;
using Mediator;
using Moq;

namespace GarageFlow.Tests.Unit.Common;

public class MediatorDomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldPublishDomainEventNotification_ForEachDomainEvent()
    {
        var mediatorMock = new Mock<IMediator>();
        var domainEvent = new TestDomainEvent();

        mediatorMock
            .Setup(x => x.Publish(
                It.Is<INotification>(notification =>
                    IsDomainEventNotification(notification, domainEvent)),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var dispatcher = new MediatorDomainEventDispatcher(mediatorMock.Object);

        await dispatcher.DispatchAsync([domainEvent], CancellationToken.None);

        mediatorMock.Verify(
            x => x.Publish(
                It.Is<INotification>(notification =>
                    IsDomainEventNotification(notification, domainEvent)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static bool IsDomainEventNotification(INotification notification, DomainEvent domainEvent)
    {
        return notification is DomainEventNotification domainEventNotification
            && ReferenceEquals(domainEvent, domainEventNotification.DomainEvent);
    }

    private sealed record TestDomainEvent : DomainEvent;
}
