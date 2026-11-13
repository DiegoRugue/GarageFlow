using System.Net;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.UseCases.ReceiveEstimateDecision;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Shared.InventoryItems;
using GarageFlow.Tests.Shared.WorkOrders;
using Mediator;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GarageFlow.Tests.Integration.WorkOrders;

public sealed class ExecutionSchedulingSagaTests
{
    [Fact]
    public async Task SchedulingOnlyConfigurationStartsTheOutboxPublisher()
    {
        var polled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new Mock<IIntegrationOutboxProcessor>(MockBehavior.Strict);
        processor.Setup(value => value.ProcessBatchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                polled.TrySetResult();
                return new IntegrationOutboxProcessingResult(0, 0, 0, 0);
            });

        var queue = new Mock<IAmazonSQS>(MockBehavior.Strict);
        queue.Setup(client => client.ReceiveMessageAsync(
                It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (ReceiveMessageRequest _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new ReceiveMessageResponse();
            });

        using var factory = new GarageFlowWebApplicationFactory(
            $"execution-only-{Guid.NewGuid():N}",
            additionalSettings: new Dictionary<string, string?>
            {
                ["Integrations:ExecutionScheduling:Enabled"] = "true",
                ["Integrations:ExecutionScheduling:CommandQueueUrl"] = "https://sqs.example.test/commands",
                ["Integrations:ExecutionScheduling:ResultQueueUrl"] = "https://sqs.example.test/results"
            });
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(queue.Object);
            services.AddSingleton(processor.Object);
        }));

        _ = host.Services;
        await polled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(true, WorkOrderStatus.ExecutionScheduled, 23)]
    [InlineData(false, WorkOrderStatus.ExecutionScheduleRejected, 25)]
    public async Task ApprovalPublishesScheduleAndResultIsAppliedExactlyOnce(
        bool accepted, WorkOrderStatus expectedStatus, int expectedStock)
    {
        using var factory = new GarageFlowWebApplicationFactory($"execution-{Guid.NewGuid():N}");
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton(new ExecutionSchedulingSettings(true))));
        var services = host.Services;
        var cancellationToken = CancellationToken.None;
        var inventory = new InventoryItemBuilder().WithStockQuantity(25).Build();
        inventory.DecreaseStock(2);
        var order = new WorkOrderBuilder().BuildCreated();
        var estimate = order.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(order, estimate.Id);
        order.AddInventoryLine(estimate.Id, inventory.Id, inventory.Description,
            EstimateItemQuantity.Create(2), inventory.Cost, inventory.Price);
        order.SubmitEstimate(estimate.Id);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
            db.WorkOrders.Add(order);
            db.InventoryItems.Add(inventory);
            await db.SaveChangesAsync(cancellationToken);
        }

        Guid executionId;
        await using (var scope = services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var approval = new ReceiveEstimateDecisionCommand(
                Guid.NewGuid(), order.Id.Value, estimate.Id.Value, "Approved", DateTime.UtcNow, new string('a', 64));
            await mediator.Send(approval, cancellationToken);
            await mediator.Send(approval, cancellationToken);

            var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
            var stored = await db.WorkOrders.SingleAsync(cancellationToken);
            Assert.Equal(WorkOrderStatus.WaitingExecutionSchedule, stored.Status);
            Assert.Null(stored.StartedAt);
            executionId = Assert.IsType<Guid>(stored.ExecutionId);
            var message = await db.IntegrationOutboxMessages.SingleAsync(
                row => row.EventKey == "execution.schedule.v1", cancellationToken);
            Assert.Equal(executionId, message.Id);
            using var body = JsonDocument.Parse(message.Payload);
            var root = body.RootElement;
            Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
            Assert.Equal("execution.schedule.v1", root.GetProperty("messageType").GetString());
            Assert.Equal(order.Id.Value, root.GetProperty("correlationId").GetGuid());
            Assert.Equal(estimate.Id.Value, root.GetProperty("estimateId").GetGuid());
            Assert.Equal(executionId, root.GetProperty("executionId").GetGuid());
            Assert.Equal(estimate.ServiceLines.Single().Id.Value,
                root.GetProperty("payload").GetProperty("serviceLineIds")[0].GetGuid());
        }

        var queue = new Mock<IAmazonSQS>(MockBehavior.Strict);
        var options = new ExecutionSchedulingQueueOptions { ResultQueueUrl = "https://sqs.example.test/results" };
        queue.Setup(client => client.DeleteMessageAsync(
                It.Is<DeleteMessageRequest>(request =>
                    request.QueueUrl == options.ResultQueueUrl && request.ReceiptHandle == "receipt"),
                cancellationToken))
            .ReturnsAsync(new DeleteMessageResponse());
        var consumer = new SqsWorkOrderResultConsumer(
            queue.Object,
            services.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<SqsWorkOrderResultConsumer>.Instance);
        var invalid = CreateResult(order.Id.Value, estimate.Id.Value, Guid.NewGuid(), accepted);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => consumer.ApplyAndAcknowledgeAsync(invalid, cancellationToken));
        queue.VerifyNoOtherCalls();

        var result = CreateResult(order.Id.Value, estimate.Id.Value, executionId, accepted);
        await consumer.ApplyAndAcknowledgeAsync(result, cancellationToken);
        await consumer.ApplyAndAcknowledgeAsync(result, cancellationToken);
        queue.Verify(client => client.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), cancellationToken),
            Times.Exactly(2));

        await using var verificationScope = services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var persistedOrder = await verificationDb.WorkOrders.SingleAsync(cancellationToken);
        var persistedInventory = await verificationDb.InventoryItems.SingleAsync(cancellationToken);
        Assert.Equal(expectedStatus, persistedOrder.Status);
        Assert.Null(persistedOrder.StartedAt);
        Assert.Equal(expectedStock, persistedInventory.StockQuantity.Value);
        Assert.Equal(1, await verificationDb.IntegrationOutboxMessages.CountAsync(
            row => row.EventKey == WorkOrderStatusChangedIntegrationEvent.EventKey &&
                row.Payload.Contains(expectedStatus.ToString()), cancellationToken));
    }

    [Fact]
    public async Task GivenPendingEstimate_WhenRepairCompletes_ThenOnlyVerifiedPaymentAllowsDelivery()
    {
        using var factory = new GarageFlowWebApplicationFactory($"execution-lifecycle-{Guid.NewGuid():N}");
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton(new ExecutionSchedulingSettings(true))));
        using var client = host.CreateClient();
        var services = host.Services;
        var order = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimateId = order.Estimates.Single().Id;

        Guid executionId;

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
            db.WorkOrders.Add(order);
            await db.SaveChangesAsync();

            var approval = new ReceiveEstimateDecisionCommand(
                Guid.NewGuid(), order.Id.Value, estimateId.Value, "Approved", DateTime.UtcNow, new string('a', 64));
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(approval, CancellationToken.None);
            var command = await db.IntegrationOutboxMessages.SingleAsync(row => row.EventKey == "execution.schedule.v1");
            executionId = command.Id;
        }

        var orderPath = $"/work-orders/{order.Id.Value:D}";
        using (var anonymous = await client.PostAsync($"{orderPath}/execution/start", null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        await client.AuthenticateAsActiveBootstrapAdminAsync();
        var queue = new Mock<IAmazonSQS>(MockBehavior.Strict);
        queue.Setup(service => service.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), CancellationToken.None))
            .ReturnsAsync(new DeleteMessageResponse());
        using var consumer = new SqsWorkOrderResultConsumer(
            queue.Object, services.GetRequiredService<IServiceScopeFactory>(),
            new ExecutionSchedulingQueueOptions
            {
                ResultQueueUrl = "https://sqs.example.test/execution-results",
                BillingResultQueueUrl = "https://sqs.example.test/billing-results"
            },
            NullLogger<SqsWorkOrderResultConsumer>.Instance);
        var scheduled = CreateResult(order.Id.Value, estimateId.Value, executionId, accepted: true);
        await consumer.ApplyAndAcknowledgeAsync(scheduled, CancellationToken.None);

        using (var premature = await client.PostAsync($"{orderPath}/execution/complete", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        }

        for (var retry = 0; retry < 2; retry++)
        {
            using var request = await client.PostAsync($"{orderPath}/execution/start", null);
            Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        }

        var startedAt = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        var completedAt = startedAt.AddHours(1);
        var start = CreateLifecycleResult(order.Id.Value, estimateId.Value, executionId, startedAt, "execution.started.v1");
        await consumer.ApplyAndAcknowledgeAsync(start, CancellationToken.None);
        using (var request = await client.PostAsync($"{orderPath}/execution/complete", null))
        {
            Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        }

        var staleCompletion = CreateLifecycleResult(
            order.Id.Value, estimateId.Value, executionId, startedAt.AddSeconds(-1), "execution.completed.v1");
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => consumer.ApplyAndAcknowledgeAsync(staleCompletion, CancellationToken.None));
        var completion = CreateLifecycleResult(order.Id.Value, estimateId.Value, executionId, completedAt, "execution.completed.v1");
        await consumer.ApplyAndAcknowledgeAsync(completion, CancellationToken.None);
        await consumer.ApplyAndAcknowledgeAsync(completion, CancellationToken.None);
        await consumer.ApplyAndAcknowledgeAsync(start, CancellationToken.None);
        await consumer.ApplyAndAcknowledgeAsync(scheduled, CancellationToken.None);
        queue.Verify(service => service.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), CancellationToken.None),
            Times.Exactly(6));

        using (var delivery = await client.PostAsync($"{orderPath}/deliver", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, delivery.StatusCode);
        }

        await using var verificationScope = services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var persisted = await verificationDb.WorkOrders.Include(item => item.Estimates)
            .ThenInclude(item => item.ServiceLines).SingleAsync();
        Assert.Equal(WorkOrderStatus.Completed, persisted.Status);
        Assert.Equal(startedAt.UtcDateTime, persisted.StartedAt);
        Assert.Equal(completedAt.UtcDateTime, persisted.CompletedAt);
        var line = persisted.Estimates.Single().ServiceLines.Single();
        Assert.Equal(EstimateServiceLineStatus.Completed, line.Status);
        Assert.Equal(startedAt.UtcDateTime, line.StartedAt);
        Assert.Equal(completedAt.UtcDateTime, line.CompletedAt);
        var outbox = verificationDb.IntegrationOutboxMessages;
        Assert.Equal(1, await outbox.CountAsync(row => row.EventKey == "execution.start.v1"));
        Assert.Equal(1, await outbox.CountAsync(row => row.EventKey == "execution.complete.v1"));
        var charge = await outbox.SingleAsync(row => row.EventKey == "billing.create-charge.v1");
        using var body = JsonDocument.Parse(charge.Payload);
        Assert.Equal(executionId, body.RootElement.GetProperty("chargeId").GetGuid());
        Assert.Equal(estimateId.Value, body.RootElement.GetProperty("estimateId").GetGuid());
        Assert.Equal(12000, body.RootElement.GetProperty("payload").GetProperty("amountInMinorUnits").GetInt32());
        Assert.Equal("BRL", body.RootElement.GetProperty("payload").GetProperty("currency").GetString());

        const string checkoutUrl = "https://www.mercadopago.com.br/checkout/v1/redirect?order_id=ORDTST001";
        var checkout = CreateBillingResult(order.Id.Value, estimateId.Value, executionId, "billing.checkout-created.v1",
            new { amountInMinorUnits = 12000, currency = "BRL", checkoutUrl });
        await consumer.ApplyBillingAndAcknowledgeAsync(checkout, CancellationToken.None);
        using (var checkoutOnly = await client.PostAsync($"{orderPath}/deliver", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, checkoutOnly.StatusCode);
        }

        var paid = CreateBillingResult(order.Id.Value, estimateId.Value, executionId, "billing.payment-state.v1",
            new { amountInMinorUnits = 12000, currency = "BRL", revision = 1, state = "paid" });
        await consumer.ApplyBillingAndAcknowledgeAsync(paid, CancellationToken.None);

        using var delivered = await client.PostAsync($"{orderPath}/deliver", null);
        Assert.Equal(HttpStatusCode.NoContent, delivered.StatusCode);

        var deliveredOrder = await verificationDb.WorkOrders.AsNoTracking().SingleAsync();
        Assert.Equal(WorkOrderStatus.Delivered, deliveredOrder.Status);
        queue.Verify(service => service.DeleteMessageAsync(
                It.Is<DeleteMessageRequest>(request => request.QueueUrl == "https://sqs.example.test/billing-results"),
                CancellationToken.None),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BillingCheckoutAndVerifiedPaymentUnlockDeliveryAndIgnoreStaleConfirmationAfterRefund()
    {
        using var factory = new GarageFlowWebApplicationFactory($"billing-{Guid.NewGuid():N}");
        using var client = factory.CreateClient();
        var services = factory.Services;
        var order = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimateId = order.Estimates.Single().Id;
        order.ApproveEstimate(estimateId, scheduleExecution: true);
        var chargeId = Assert.IsType<Guid>(order.ExecutionId);
        var start = DateTime.UtcNow;
        order.ApplyExecutionScheduleDecision(estimateId, chargeId, accepted: true);
        order.RequestExecutionStart();
        order.ApplyExecutionStarted(estimateId, chargeId, start);
        order.RequestExecutionCompletion();
        order.ApplyExecutionCompleted(estimateId, chargeId, start.AddMinutes(20));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
            db.WorkOrders.Add(order);
            await db.SaveChangesAsync();
        }

        var path = $"/work-orders/{order.Id.Value:D}";
        using (var anonymous = await client.GetAsync($"{path}/payment"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        await client.AuthenticateAsActiveBootstrapAdminAsync();
        var queue = new Mock<IAmazonSQS>(MockBehavior.Strict);
        queue.Setup(service => service.DeleteMessageAsync(
                It.Is<DeleteMessageRequest>(request => request.QueueUrl == "https://sqs.example.test/billing-results"),
                CancellationToken.None))
            .ReturnsAsync(new DeleteMessageResponse());
        using var consumer = new SqsWorkOrderResultConsumer(
            queue.Object, services.GetRequiredService<IServiceScopeFactory>(),
            new ExecutionSchedulingQueueOptions { BillingResultQueueUrl = "https://sqs.example.test/billing-results" },
            NullLogger<SqsWorkOrderResultConsumer>.Instance);

        Message BillingResult(string type, object payload) =>
            CreateBillingResult(order.Id.Value, estimateId.Value, chargeId, type, payload);

        const string checkoutUrl = "https://www.mercadopago.com.br/checkout/v1/redirect?order_id=ORDTST001";
        var checkout = BillingResult("billing.checkout-created.v1",
            new { amountInMinorUnits = 12000, currency = "BRL", checkoutUrl });
        await consumer.ApplyBillingAndAcknowledgeAsync(checkout, CancellationToken.None);
        using (var available = await client.GetAsync($"{path}/payment"))
        {
            Assert.Equal(HttpStatusCode.OK, available.StatusCode);
            using var content = JsonDocument.Parse(await available.Content.ReadAsStringAsync());
            Assert.Equal("CheckoutAvailable", content.RootElement.GetProperty("status").GetString());
            Assert.Equal(checkoutUrl, content.RootElement.GetProperty("checkoutUrl").GetString());
        }

        var incorrect = BillingResult("billing.payment-state.v1",
            new { amountInMinorUnits = 11000, currency = "BRL", revision = 1, state = "paid" });
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => consumer.ApplyBillingAndAcknowledgeAsync(incorrect, CancellationToken.None));

        var paid = BillingResult("billing.payment-state.v1",
            new { amountInMinorUnits = 12000, currency = "BRL", revision = 1, state = "paid" });
        var revoked = BillingResult("billing.payment-state.v1",
            new { amountInMinorUnits = 12000, currency = "BRL", revision = 2, state = "revoked" });
        await consumer.ApplyBillingAndAcknowledgeAsync(paid, CancellationToken.None);
        await consumer.ApplyBillingAndAcknowledgeAsync(revoked, CancellationToken.None);
        await consumer.ApplyBillingAndAcknowledgeAsync(paid, CancellationToken.None);
        using (var blocked = await client.PostAsync($"{path}/deliver", null))
        {
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        }

        var confirmed = BillingResult("billing.payment-state.v1",
            new { amountInMinorUnits = 12000, currency = "BRL", revision = 3, state = "paid" });
        await consumer.ApplyBillingAndAcknowledgeAsync(confirmed, CancellationToken.None);
        using (var delivered = await client.PostAsync($"{path}/deliver", null))
        {
            Assert.Equal(HttpStatusCode.NoContent, delivered.StatusCode);
        }

        queue.Verify(service => service.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), CancellationToken.None),
            Times.Exactly(5));
    }

    private static Message CreateBillingResult(Guid workOrderId, Guid estimateId, Guid chargeId, string type, object payload) => new()
    {
        ReceiptHandle = "billing-result",
        Body = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            messageType = type,
            messageId = Guid.NewGuid(),
            correlationId = workOrderId,
            occurredAt = DateTimeOffset.UtcNow,
            workOrderId,
            estimateId,
            chargeId,
            payload
        })
    };

    private static Message CreateLifecycleResult(
        Guid workOrderId, Guid estimateId, Guid executionId, DateTimeOffset occurredAt, string messageType) => new()
    {
        ReceiptHandle = "lifecycle-result",
        Body = JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            messageType,
            messageId = Guid.NewGuid(),
            correlationId = workOrderId,
            occurredAt,
            workOrderId,
            estimateId,
            executionId,
            payload = new { }
        })
    };

    private static Message CreateResult(Guid workOrderId, Guid estimateId, Guid executionId, bool accepted)
    {
        object payload = accepted ? new { } : new { reasonCode = "work_order_already_scheduled" };
        return new Message
        {
            ReceiptHandle = "receipt",
            Body = JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0",
                messageType = accepted ? "execution.scheduled.v1" : "execution.schedule-rejected.v1",
                messageId = Guid.NewGuid(),
                correlationId = workOrderId,
                occurredAt = DateTimeOffset.UtcNow,
                workOrderId,
                estimateId,
                executionId,
                payload
            })
        };
    }
}
