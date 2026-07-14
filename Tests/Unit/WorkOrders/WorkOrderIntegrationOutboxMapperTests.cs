using System.Text.Json;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class WorkOrderIntegrationOutboxMapperTests
{
    [Fact]
    public void Map_ShouldCreateStableCamelCaseStatusChangedMessage()
    {
        var workOrderId = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 7, 11, 10, 30, 0, DateTimeKind.Utc);
        var domainEvent = new WorkOrderStatusChanged(
            WorkOrderId.From(workOrderId),
            WorkOrderStatus.Received,
            WorkOrderStatus.Diagnosing,
            occurredAt);
        var mapper = new WorkOrderIntegrationOutboxMapper();

        var message = Assert.Single(mapper.Map([domainEvent], "correlation-42"));

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(WorkOrderStatusChangedIntegrationEvent.EventKey, message.EventKey);
        Assert.Equal(workOrderId, message.AggregateId);
        Assert.Equal(occurredAt, message.OccurredAt);
        Assert.Equal("correlation-42", message.CorrelationId);

        using var payload = JsonDocument.Parse(message.Payload);
        var root = payload.RootElement;
        Assert.Equal(workOrderId, root.GetProperty("workOrderId").GetGuid());
        Assert.Equal("Received", root.GetProperty("previousStatus").GetString());
        Assert.Equal("Diagnosing", root.GetProperty("currentStatus").GetString());
        Assert.Equal(occurredAt, root.GetProperty("occurredAt").GetDateTime());
        Assert.Equal(4, root.EnumerateObject().Count());
    }

    [Fact]
    public void Map_ShouldIgnoreUnrelatedDomainEvents()
    {
        var mapper = new WorkOrderIntegrationOutboxMapper();

        var messages = mapper.Map([new UnrelatedDomainEvent()], "correlation-42");

        Assert.Empty(messages);
    }

    [Fact]
    public void Map_ShouldGenerateANewMessageIdForEachMapping()
    {
        var domainEvent = new WorkOrderStatusChanged(
            WorkOrderId.From(Guid.NewGuid()),
            WorkOrderStatus.Received,
            WorkOrderStatus.Diagnosing,
            DateTime.UtcNow);
        var mapper = new WorkOrderIntegrationOutboxMapper();

        var first = Assert.Single(mapper.Map([domainEvent], null));
        var second = Assert.Single(mapper.Map([domainEvent], null));

        Assert.NotEqual(first.Id, second.Id);
    }

    private sealed record UnrelatedDomainEvent : DomainEvent;
}
