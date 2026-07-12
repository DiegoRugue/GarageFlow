using System.Net;
using System.Security.Cryptography;
using System.Text;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GarageFlow.Tests.E2E.WorkOrders;

public sealed class EstimateDecisionWebhookE2eTests(E2eApiFixture fixture)
    : IClassFixture<E2eApiFixture>
{
    private const int ReservedQuantity = 3;
    private const int InitialStock = 10;
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task ConcurrentDuplicateApproval_ShouldCommitInboxTransitionAndOneStableOutbox()
    {
        var setup = await SeedWaitingApprovalAsync(includeInventory: false);
        var eventId = Guid.NewGuid();
        var body = CreateBody(eventId, setup.WorkOrderId, setup.EstimateId, "Approved");
        using var client = _fixture.CreateClient();

        var responses = await Task.WhenAll(
            SendSignedAsync(client, body),
            SendSignedAsync(client, body));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        var snapshot = await ReadSnapshotAsync(setup.WorkOrderId, eventId);
        Assert.Equal(WorkOrderStatus.InProgress, snapshot.Status);
        Assert.Equal(1, snapshot.InboxCount);
        var outbox = Assert.Single(snapshot.Outbox);
        Assert.Equal(WorkOrderStatusChangedIntegrationEvent.EventKey, outbox.EventKey);
        var notification = IntegrationEventJson.Deserialize<WorkOrderStatusChangedIntegrationEvent>(outbox.Payload);
        Assert.NotNull(notification);
        Assert.Equal(setup.WorkOrderId, notification.WorkOrderId);
        Assert.Equal("WaitingApproval", notification.PreviousStatus);
        Assert.Equal("InProgress", notification.CurrentStatus);

        var conflictingBody = CreateBody(eventId, setup.WorkOrderId, setup.EstimateId, "Rejected");
        using var conflict = await SendSignedAsync(client, conflictingBody);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var afterConflict = await ReadSnapshotAsync(setup.WorkOrderId, eventId);
        Assert.Equal(outbox.Id, Assert.Single(afterConflict.Outbox).Id);
        Assert.Equal(WorkOrderStatus.InProgress, afterConflict.Status);
    }

    [Fact]
    public async Task DuplicateRejection_ShouldRestoreReservedStockExactlyOnce()
    {
        var setup = await SeedWaitingApprovalAsync(includeInventory: true);
        var eventId = Guid.NewGuid();
        var body = CreateBody(eventId, setup.WorkOrderId, setup.EstimateId, "Rejected");
        using var client = _fixture.CreateClient();

        using var first = await SendSignedAsync(client, body);
        using var duplicate = await SendSignedAsync(client, body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, duplicate.StatusCode);
        var snapshot = await ReadSnapshotAsync(setup.WorkOrderId, eventId);
        Assert.Equal(WorkOrderStatus.Diagnosing, snapshot.Status);
        Assert.Equal(1, snapshot.InboxCount);
        Assert.Single(snapshot.Outbox);
        Assert.Equal(InitialStock, await ReadStockAsync(setup.InventoryItemId!.Value));
    }

    [Fact]
    public async Task InvalidAndExpiredSignatures_ShouldNotCreateInboxDomainOrOutboxRows()
    {
        var setup = await SeedWaitingApprovalAsync(includeInventory: false);
        using var client = _fixture.CreateClient();
        var invalidEventId = Guid.NewGuid();
        var invalidBody = CreateBody(invalidEventId, setup.WorkOrderId, setup.EstimateId, "Approved");

        using var invalid = await SendAsync(
            client,
            invalidBody,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new string('0', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        var expiredEventId = Guid.NewGuid();
        var expiredBody = CreateBody(expiredEventId, setup.WorkOrderId, setup.EstimateId, "Approved");
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        using var expired = await SendAsync(
            client,
            expiredBody,
            expiredTimestamp,
            ComputeSignature(expiredTimestamp, expiredBody));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        var invalidSnapshot = await ReadSnapshotAsync(setup.WorkOrderId, invalidEventId);
        var expiredSnapshot = await ReadSnapshotAsync(setup.WorkOrderId, expiredEventId);
        Assert.Equal(WorkOrderStatus.WaitingApproval, invalidSnapshot.Status);
        Assert.Equal(0, invalidSnapshot.InboxCount);
        Assert.Empty(invalidSnapshot.Outbox);
        Assert.Equal(0, expiredSnapshot.InboxCount);
        Assert.Empty(expiredSnapshot.Outbox);
    }

    [Fact]
    public async Task FailedTransition_ShouldRollBackAtomically_AndCorrectedSameEventIdCanSucceed()
    {
        var setup = await SeedWaitingApprovalAsync(includeInventory: false);
        var eventId = Guid.NewGuid();
        using var client = _fixture.CreateClient();
        var invalidBody = CreateBody(eventId, setup.WorkOrderId, Guid.NewGuid(), "Approved");

        using var failed = await SendSignedAsync(client, invalidBody);
        Assert.Equal(HttpStatusCode.NotFound, failed.StatusCode);
        var rolledBack = await ReadSnapshotAsync(setup.WorkOrderId, eventId);
        Assert.Equal(WorkOrderStatus.WaitingApproval, rolledBack.Status);
        Assert.Equal(0, rolledBack.InboxCount);
        Assert.Empty(rolledBack.Outbox);

        var correctedBody = CreateBody(eventId, setup.WorkOrderId, setup.EstimateId, "Approved");
        using var corrected = await SendSignedAsync(client, correctedBody);
        Assert.Equal(HttpStatusCode.NoContent, corrected.StatusCode);
        var committed = await ReadSnapshotAsync(setup.WorkOrderId, eventId);
        Assert.Equal(WorkOrderStatus.InProgress, committed.Status);
        Assert.Equal(1, committed.InboxCount);
        Assert.Single(committed.Outbox);
    }

    private async Task<SeededEstimate> SeedWaitingApprovalAsync(bool includeInventory)
    {
        var seed = Guid.NewGuid().ToString("N");
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var customer = Customer.Create(
            TaxDocument.Create(GenerateValidCpf(seed)),
            FullName.Create("Webhook Acceptance Customer"),
            Email.Create($"webhook-{seed}@garageflow.local"),
            PhoneNumber.Create($"11{Math.Abs(seed.GetHashCode(StringComparison.Ordinal)) % 1_000_000_000:D9}"));
        var brand = VehicleBrand.Create($"Webhook Brand {seed[..8]}");
        var model = VehicleModel.Create(brand.Id, $"Webhook Model {seed[..8]}");
        var color = VehicleColor.Create($"Webhook Color {seed[..8]}");
        var vehicle = Vehicle.Create(
            customer.Id,
            2025,
            brand.Id,
            model.Id,
            color.Id,
            LicensePlate.Create(CreatePlate(seed)));
        var service = GarageFlow.Domain.Services.Entities.Service.Create(
            Description.Create("Webhook acceptance service"),
            Price.Create(125m));
        InventoryItem? inventory = null;
        if (includeInventory)
        {
            inventory = InventoryItem.Create(
                InventoryItemName.Create($"Webhook item {seed[..8]}"),
                Description.Create("Webhook acceptance inventory"),
                InventoryItemType.Part,
                Price.Create(10m),
                Price.Create(20m),
                InventoryItemStockQuantity.Create(InitialStock));
        }

        var workOrder = WorkOrder.Create(customer.Id, vehicle.Id);
        var estimate = workOrder.CreateEstimate();
        workOrder.AddServiceLine(estimate.Id, service.Id, service.Description, service.Price);
        if (inventory is not null)
        {
            workOrder.AddInventoryLine(
                estimate.Id,
                inventory.Id,
                inventory.Description,
                EstimateItemQuantity.Create(ReservedQuantity),
                inventory.Cost,
                inventory.Price);
            inventory.DecreaseStock(ReservedQuantity);
        }

        workOrder.SubmitEstimate(estimate.Id);
        context.AddRange(customer, brand, model, color, vehicle, service, workOrder);
        if (inventory is not null)
        {
            context.InventoryItems.Add(inventory);
        }

        await context.SaveChangesAsync();
        return new SeededEstimate(
            workOrder.Id.Value,
            estimate.Id.Value,
            inventory?.Id.Value);
    }

    private async Task<Snapshot> ReadSnapshotAsync(Guid workOrderId, Guid eventId)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var status = await context.WorkOrders.AsNoTracking()
            .Where(workOrder => workOrder.Id == GarageFlow.Domain.WorkOrders.ValueObjects.WorkOrderId.From(workOrderId))
            .Select(workOrder => workOrder.Status)
            .SingleAsync();
        var inboxCount = await context.EstimateDecisionInboxEvents.AsNoTracking()
            .CountAsync(row => row.EventId == eventId);
        var outbox = await context.IntegrationOutboxMessages.AsNoTracking()
            .Where(row => row.AggregateId == workOrderId)
            .OrderBy(row => row.OccurredAt)
            .ToListAsync();
        return new Snapshot(status, inboxCount, outbox);
    }

    private async Task<int> ReadStockAsync(Guid inventoryItemId)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        return await context.InventoryItems.AsNoTracking()
            .Where(item => item.Id == InventoryItemId.From(inventoryItemId))
            .Select(item => item.StockQuantity.Value)
            .SingleAsync();
    }

    private static byte[] CreateBody(
        Guid eventId,
        Guid workOrderId,
        Guid estimateId,
        string decision) => Encoding.UTF8.GetBytes(
        $$"""
        { "eventId": "{{eventId}}", "workOrderId": "{{workOrderId}}", "estimateId": "{{estimateId}}", "decision": "{{decision}}", "occurredAt": "2026-07-12T12:00:00.0000000Z" }
        """);

    private static Task<HttpResponseMessage> SendSignedAsync(HttpClient client, byte[] body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return SendAsync(client, body, timestamp, ComputeSignature(timestamp, body));
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        byte[] body,
        long timestamp,
        string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/estimate-decisions")
        {
            Content = new ByteArrayContent(body)
        };
        request.Headers.Add("X-GarageFlow-Timestamp", timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("X-GarageFlow-Signature", signature);
        return await client.SendAsync(request);
    }

    private static string ComputeSignature(long timestamp, byte[] body)
    {
        var prefix = Encoding.ASCII.GetBytes(
            timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed, prefix.Length);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(E2eAuthSettings.EstimateDecisionWebhookSecret));
        return Convert.ToHexStringLower(hmac.ComputeHash(signed));
    }

    private static string GenerateValidCpf(string seed)
    {
        var digits = seed.Select(character => char.IsDigit(character) ? character - '0' : character % 10)
            .Take(9).ToArray();
        if (digits.Distinct().Count() == 1)
        {
            digits[8] = (digits[8] + 1) % 10;
        }

        var first = CalculateCpfDigit(digits, 10);
        var ten = digits.Append(first).ToArray();
        var second = CalculateCpfDigit(ten, 11);
        return string.Concat(ten.Append(second));
    }

    private static int CalculateCpfDigit(IReadOnlyList<int> digits, int weight)
    {
        var remainder = digits.Select((digit, index) => digit * (weight - index)).Sum() % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static string CreatePlate(string seed) =>
        $"{(char)('A' + seed[0] % 26)}{(char)('A' + seed[1] % 26)}{(char)('A' + seed[2] % 26)}{seed[3] % 10}{(char)('A' + seed[4] % 26)}{seed[5] % 10}{seed[6] % 10}";

    private sealed record SeededEstimate(Guid WorkOrderId, Guid EstimateId, Guid? InventoryItemId);
    private sealed record Snapshot(
        WorkOrderStatus Status,
        int InboxCount,
        IReadOnlyList<IntegrationOutboxMessageEntity> Outbox);
}
