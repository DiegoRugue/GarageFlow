using GarageFlow.Application.Common.Events;
using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Tests.Integration.Support.Factories;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace GarageFlow.Tests.Integration.Common;

public sealed class DomainEventDispatchIntegrationTests
{
    [Fact]
    public async Task Dispatcher_ShouldAllowZeroNotificationHandlers()
    {
        await using var factory = new GarageFlowWebApplicationFactory(
            $"garageflow-domain-dispatch-{Guid.NewGuid():N}");
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var handlers = scope.ServiceProvider
            .GetServices<INotificationHandler<DomainEventNotification>>()
            .ToList();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        Assert.Empty(handlers);
        await dispatcher.DispatchAsync([new TestDomainEvent()], CancellationToken.None);
    }

    private sealed record TestDomainEvent : DomainEvent;
}
