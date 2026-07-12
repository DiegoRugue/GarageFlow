using System.Net;
using System.Security.Cryptography;
using System.Text;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Inbox;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GarageFlow.Tests.E2E.WorkOrders;

public sealed class EstimateDecisionInboxPostgresE2eTests(E2eApiFixture fixture)
    : IClassFixture<E2eApiFixture>
{
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task EstimateDecisionInbox_ShouldReleaseConcurrentClaim_WhenWinnerRollsBack()
    {
        using var startupClient = _fixture.CreateClient();
        using var winnerScope = _fixture.CreateScope();
        using var loserScope = _fixture.CreateScope();
        var winnerContext = winnerScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var loserContext = loserScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var winner = winnerScope.ServiceProvider.GetRequiredService<IEstimateDecisionInbox>();
        var loser = loserScope.ServiceProvider.GetRequiredService<IEstimateDecisionInbox>();
        var eventId = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        await winnerContext.BeginTransactionAsync();
        var winnerResult = await winner.RegisterAsync(eventId, new string('a', 64), occurredAt, occurredAt);
        Assert.True(winnerResult.IsNew);

        await loserContext.BeginTransactionAsync();
        var loserConnection = (NpgsqlConnection)loserContext.Database.GetDbConnection();
        var loserBackendProcessId = loserConnection.ProcessID;
        var loserTask = loser.RegisterAsync(eventId, new string('b', 64), occurredAt, occurredAt);
        await WaitUntilBackendIsBlockedOnLockAsync(loserBackendProcessId);

        await winnerContext.RollbackTransactionAsync();
        var loserResult = await loserTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(loserResult.IsNew);
        await loserContext.CommitTransactionAsync();
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldRollBackInboxInsert_WhenProcessingFails()
    {
        using var client = _fixture.CreateClient();
        var eventId = Guid.NewGuid();
        var body = Encoding.UTF8.GetBytes($$"""
            {"eventId":"{{eventId}}","workOrderId":"{{Guid.NewGuid()}}","estimateId":"{{Guid.NewGuid()}}","decision":"Approved","occurredAt":"2026-07-11T12:00:00Z"}
            """);

        using var response = await SendSignedAsync(client, body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"EstimateDecisionInboxEvents\" WHERE \"EventId\" = @eventId",
            connection);
        command.Parameters.AddWithValue("eventId", eventId);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    private async Task WaitUntilBackendIsBlockedOnLockAsync(int backendProcessId)
    {
        await using var observer = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await observer.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT "wait_event_type" = 'Lock'
            FROM "pg_stat_activity"
            WHERE "pid" = @backendProcessId
            """,
            observer);
        command.Parameters.AddWithValue("backendProcessId", backendProcessId);

        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(10);
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            if (await command.ExecuteScalarAsync() is true)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"PostgreSQL backend {backendProcessId} did not enter a lock wait within the bounded polling window.");
    }

    private static async Task<HttpResponseMessage> SendSignedAsync(HttpClient client, byte[] body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var prefix = Encoding.ASCII.GetBytes(timestamp + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed, prefix.Length);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(E2eAuthSettings.EstimateDecisionWebhookSecret));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/estimate-decisions")
        {
            Content = new ByteArrayContent(body)
        };
        request.Headers.Add("X-GarageFlow-Timestamp", timestamp);
        request.Headers.Add("X-GarageFlow-Signature", Convert.ToHexStringLower(hmac.ComputeHash(signed)));
        return await client.SendAsync(request);
    }
}
