using System.Net;
using System.Security.Cryptography;
using System.Text;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GarageFlow.Tests.E2E.Integrations;

[Collection(OutboxLifecycleE2eCollectionDefinition.Name)]
public sealed class OutboxConcurrencyE2eTests(OutboxLifecycleE2eFixture fixture)
{
    private readonly OutboxLifecycleE2eFixture _fixture = fixture;

    [Fact]
    public async Task RealWebhookTransition_ShouldRemainPendingDuringPublish_ThenPublishExactEventAndProcess()
    {
        await ResetAsync();
        var setup = await SeedWaitingApprovalAsync();
        var eventId = Guid.NewGuid();
        var body = CreateWebhookBody(eventId, setup.WorkOrderId, setup.EstimateId, "Approved");
        _fixture.Publisher.BlockNextAttempt();
        using var client = _fixture.CreateClient();

        try
        {
            using var response = await SendSignedWebhookAsync(client, body);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            _fixture.AdvanceClock(TimeSpan.FromMinutes(1));
            await _fixture.Publisher.WaitForBlockedAttemptAsync(TimeSpan.FromSeconds(10));

            var pending = await GetRowByAggregateAsync(setup.WorkOrderId);
            Assert.Null(pending.ProcessedAt);
            Assert.Equal(1, pending.AttemptCount);
            Assert.NotNull(pending.LeaseId);
            Assert.NotNull(pending.LeaseExpiresAt);
            Assert.Null(pending.LastError);
            var expected = IntegrationEventJson.Deserialize<WorkOrderStatusChangedIntegrationEvent>(pending.Payload);
            Assert.NotNull(expected);
            Assert.Equal(setup.WorkOrderId, expected.WorkOrderId);
            Assert.Equal("WaitingApproval", expected.PreviousStatus);
            Assert.Equal("InProgress", expected.CurrentStatus);

            _fixture.Publisher.ReleaseBlockedAttempt();
            var actual = await _fixture.Publisher.WaitForPublishedAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(expected, actual);
            await WaitForRowAsync(pending.Id, row => row.ProcessedAt is not null);

            var processed = await GetRowAsync(pending.Id);
            Assert.Equal(1, processed.AttemptCount);
            Assert.Null(processed.LeaseId);
            Assert.Null(processed.LeaseExpiresAt);
            Assert.Null(processed.LastError);
        }
        finally
        {
            TryReleasePublisherGate();
        }
    }

    [Fact]
    public async Task Worker_ShouldRescheduleInjectedFailure_ThenEventuallyPublishExactlyOnce()
    {
        await ResetAsync(failures: 1);
        var expected = CreateEvent();
        var firstAttemptAt = _fixture.UtcNow;
        var messageId = await InsertAsync(expected, firstAttemptAt);

        await WaitForRowAsync(messageId, row => row.AttemptCount == 1 && row.LastError != null);
        var failed = await GetRowAsync(messageId);
        Assert.Equal(1, failed.AttemptCount);
        Assert.Null(failed.ProcessedAt);
        Assert.Null(failed.LeaseId);
        Assert.Null(failed.LeaseExpiresAt);
        Assert.Contains("Injected publisher failure", failed.LastError, StringComparison.Ordinal);
        Assert.Equal(firstAttemptAt.AddHours(1), failed.NextAttemptAt);

        _fixture.Publisher.BlockNextAttempt();
        try
        {
            _fixture.AdvanceClock(TimeSpan.FromHours(1));
            await _fixture.Publisher.WaitForBlockedAttemptAsync(TimeSpan.FromSeconds(10));
            var secondAttempt = await GetRowAsync(messageId);
            Assert.Equal(2, secondAttempt.AttemptCount);
            Assert.Null(secondAttempt.ProcessedAt);
            Assert.NotNull(secondAttempt.LeaseId);

            _fixture.Publisher.ReleaseBlockedAttempt();
            var retried = await _fixture.Publisher.WaitForPublishedAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(expected, retried);
            await WaitForRowAsync(messageId, row => row.ProcessedAt is not null);
        }
        finally
        {
            TryReleasePublisherGate();
        }

        var row = await GetRowAsync(messageId);
        Assert.Equal(2, row.AttemptCount);
        Assert.Equal(2, _fixture.Publisher.Attempts.Count);
        Assert.Null(row.LastError);
        Assert.Null(row.LeaseId);
        Assert.Null(row.LeaseExpiresAt);
    }

    [Fact]
    public async Task TwoProcessors_ShouldClaimDisjointRows()
    {
        await ResetAsync();
        var repositoryNow = _fixture.UtcNow.AddHours(1);
        var ids = new[]
        {
            await InsertAsync(CreateEvent(), repositoryNow),
            await InsertAsync(CreateEvent(), repositoryNow),
            await InsertAsync(CreateEvent(), repositoryNow),
            await InsertAsync(CreateEvent(), repositoryNow)
        };

        using var firstScope = _fixture.CreateScope();
        using var secondScope = _fixture.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLease = Guid.NewGuid();
        var secondLease = Guid.NewGuid();
        var firstTask = Task.Run(async () =>
        {
            await start.Task;
            return await first.ClaimBatchAsync(firstLease, repositoryNow, repositoryNow.AddMinutes(1), 2);
        });
        var secondTask = Task.Run(async () =>
        {
            await start.Task;
            return await second.ClaimBatchAsync(secondLease, repositoryNow, repositoryNow.AddMinutes(1), 2);
        });
        start.SetResult();
        var batches = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(2, batches[0].Count);
        Assert.Equal(2, batches[1].Count);
        Assert.Empty(batches[0].Select(row => row.Id).Intersect(batches[1].Select(row => row.Id)));
        Assert.True(ids.ToHashSet().SetEquals(batches.SelectMany(batch => batch).Select(row => row.Id)));
    }

    [Fact]
    public async Task TwoProcessorInstances_ShouldPublishDisjointMessagesThroughRealPostgreSqlClaims()
    {
        await ResetAsync();
        var now = _fixture.UtcNow.AddHours(1);
        var expected = Enumerable.Range(0, 4).Select(_ => CreateEvent()).ToArray();
        foreach (var notification in expected)
        {
            _ = await InsertAsync(notification, now);
        }

        var options = Options.Create(new IntegrationOutboxOptions
        {
            Enabled = true,
            BatchSize = 2,
            PollingIntervalSeconds = 1,
            LeaseDurationSeconds = 30,
            InitialRetryDelaySeconds = 1,
            MaxRetryDelaySeconds = 2
        });
        using var firstScope = _fixture.CreateScope();
        using var secondScope = _fixture.CreateScope();
        var first = new IntegrationOutboxProcessor(
            firstScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>(),
            _fixture.Publisher,
            options,
            new FixedTimeProvider(now),
            firstScope.ServiceProvider.GetRequiredService<IntegrationOutboxTelemetry>());
        var second = new IntegrationOutboxProcessor(
            secondScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>(),
            _fixture.Publisher,
            options,
            new FixedTimeProvider(now),
            secondScope.ServiceProvider.GetRequiredService<IntegrationOutboxTelemetry>());

        var results = await Task.WhenAll(first.ProcessBatchAsync(), second.ProcessBatchAsync());

        Assert.All(results, result =>
        {
            Assert.Equal(2, result.ClaimedCount);
            Assert.Equal(2, result.ProcessedCount);
        });
        Assert.Equal(4, _fixture.Publisher.Attempts.Count);
        Assert.Equal(
            expected.Select(item => item.WorkOrderId).Order(),
            _fixture.Publisher.Attempts.Select(item => item.WorkOrderId).Order());
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"IntegrationOutboxMessages\" WHERE \"ProcessedAt\" IS NOT NULL",
            connection);
        Assert.Equal(4L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task ExpiredLease_ShouldRejectStaleOwnerAfterReclaim_AndProcessedRowNeverReclaimed()
    {
        await ResetAsync();
        var now = _fixture.UtcNow.AddHours(1);
        var reclaimId = await InsertAsync(CreateEvent(), now);
        var processedId = await InsertAsync(CreateEvent(), now);

        var initialLease = Guid.NewGuid();
        using var initialScope = _fixture.CreateScope();
        var initialRepository = initialScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();
        var initial = await initialRepository.ClaimBatchAsync(initialLease, now, now.AddSeconds(1), 2);
        Assert.Equal(2, initial.Count);
        Assert.Equal(1, initial.Single(row => row.Id == reclaimId).AttemptCount);
        Assert.True(await initialRepository.MarkProcessedAsync(processedId, initialLease, now));

        var reclaimLease = Guid.NewGuid();
        using var reclaimScope = _fixture.CreateScope();
        var reclaimRepository = reclaimScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();
        var reclaimed = Assert.Single(await reclaimRepository.ClaimBatchAsync(
            reclaimLease,
            now.AddSeconds(2),
            now.AddMinutes(1),
            2));
        Assert.Equal(reclaimId, reclaimed.Id);
        Assert.Equal(2, reclaimed.AttemptCount);
        Assert.NotEqual(processedId, reclaimed.Id);
        Assert.False(await initialRepository.MarkProcessedAsync(reclaimId, initialLease, now.AddSeconds(2)));
        Assert.False(await initialRepository.RescheduleAsync(
            reclaimId,
            initialLease,
            now.AddMinutes(2),
            "stale owner"));
        Assert.True(await reclaimRepository.MarkProcessedAsync(reclaimId, reclaimLease, now.AddSeconds(2)));

        var fresh = await GetRowAsync(reclaimId);
        Assert.Equal(2, fresh.AttemptCount);
        Assert.Equal(now.AddSeconds(2), fresh.ProcessedAt);
        Assert.Null(fresh.LeaseId);
        Assert.Null(fresh.LeaseExpiresAt);
        using var finalScope = _fixture.CreateScope();
        var finalRepository = finalScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();
        Assert.Empty(await finalRepository.ClaimBatchAsync(
            Guid.NewGuid(),
            now.AddHours(1),
            now.AddHours(1).AddMinutes(1),
            2));
    }

    private async Task<SeededEstimate> SeedWaitingApprovalAsync()
    {
        var seed = Guid.NewGuid().ToString("N");
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var customer = Customer.Create(
            TaxDocument.Create(GenerateValidCpf(seed)),
            FullName.Create("Outbox Lifecycle Customer"),
            Email.Create($"outbox-{seed}@garageflow.local"),
            PhoneNumber.Create($"11{Math.Abs(seed.GetHashCode(StringComparison.Ordinal)) % 1_000_000_000:D9}"));
        var brand = VehicleBrand.Create($"Outbox Brand {seed[..8]}");
        var model = VehicleModel.Create(brand.Id, $"Outbox Model {seed[..8]}");
        var color = VehicleColor.Create($"Outbox Color {seed[..8]}");
        var vehicle = Vehicle.Create(
            customer.Id,
            2025,
            brand.Id,
            model.Id,
            color.Id,
            LicensePlate.Create(CreatePlate(seed)));
        var service = GarageFlow.Domain.Services.Entities.Service.Create(
            Description.Create("Outbox lifecycle service"),
            Price.Create(125m));
        var workOrder = WorkOrder.Create(customer.Id, vehicle.Id);
        var estimate = workOrder.CreateEstimate();
        workOrder.AddServiceLine(estimate.Id, service.Id, service.Description, service.Price);
        workOrder.SubmitEstimate(estimate.Id);
        context.AddRange(customer, brand, model, color, vehicle, service, workOrder);
        await context.SaveChangesAsync();
        return new SeededEstimate(workOrder.Id.Value, estimate.Id.Value);
    }

    private async Task<IntegrationOutboxMessageEntity> GetRowByAggregateAsync(Guid aggregateId)
    {
        using var scope = _fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>()
            .IntegrationOutboxMessages.AsNoTracking().SingleAsync(row => row.AggregateId == aggregateId);
    }

    private async Task<HttpResponseMessage> SendSignedWebhookAsync(HttpClient client, byte[] body)
    {
        var timestamp = new DateTimeOffset(_fixture.UtcNow).ToUnixTimeSeconds();
        var prefix = Encoding.ASCII.GetBytes(
            timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed, prefix.Length);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(E2eAuthSettings.EstimateDecisionWebhookSecret));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(signed));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/estimate-decisions")
        {
            Content = new ByteArrayContent(body)
        };
        request.Headers.Add(
            "X-GarageFlow-Timestamp",
            timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("X-GarageFlow-Signature", signature);
        return await client.SendAsync(request);
    }

    private void TryReleasePublisherGate()
    {
        try
        {
            _fixture.Publisher.ReleaseBlockedAttempt();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static byte[] CreateWebhookBody(
        Guid eventId,
        Guid workOrderId,
        Guid estimateId,
        string decision) => Encoding.UTF8.GetBytes(
        $$"""
        { "eventId": "{{eventId}}", "workOrderId": "{{workOrderId}}", "estimateId": "{{estimateId}}", "decision": "{{decision}}", "occurredAt": "2026-07-12T12:00:00.0000000Z" }
        """);

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

    private async Task ResetAsync(int failures = 0)
    {
        await _fixture.Publisher.ResetAsync(failures);
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM \"IntegrationOutboxMessages\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertAsync(WorkOrderStatusChangedIntegrationEvent notification, DateTime nextAttemptAt)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "IntegrationOutboxMessages"
                ("Id", "EventKey", "AggregateId", "Payload", "OccurredAt", "AttemptCount", "NextAttemptAt")
            VALUES
                (@id, @eventKey, @aggregateId, @payload::jsonb, @occurredAt, 0, @nextAttemptAt)
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("eventKey", WorkOrderStatusChangedIntegrationEvent.EventKey);
        command.Parameters.AddWithValue("aggregateId", notification.WorkOrderId);
        command.Parameters.AddWithValue("payload", IntegrationEventJson.Serialize(notification));
        command.Parameters.AddWithValue("occurredAt", notification.OccurredAt);
        command.Parameters.AddWithValue("nextAttemptAt", nextAttemptAt);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task WaitForRowAsync(Guid id, Func<IntegrationOutboxMessageEntity, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var row = await GetRowAsync(id);
            if (condition(row))
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Outbox row {id} did not reach the expected state.");
    }

    private async Task<IntegrationOutboxMessageEntity> GetRowAsync(Guid id)
    {
        using var scope = _fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>()
            .IntegrationOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id);
    }

    private static WorkOrderStatusChangedIntegrationEvent CreateEvent() => new(
        Guid.NewGuid(),
        "WaitingApproval",
        "InProgress",
        new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc));

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed record SeededEstimate(Guid WorkOrderId, Guid EstimateId);
}
