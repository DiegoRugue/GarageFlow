using System.Net;
using System.Security.Cryptography;
using System.Text;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Tests.Shared.WorkOrders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace GarageFlow.Tests.Integration.Api.Webhooks;

public sealed class EstimateDecisionWebhookApiTests(GarageFlowApiFixture fixture)
    : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn401_WhenSignatureIsMissing()
    {
        using var client = _fixture.CreateClient();
        using var response = await client.PostAsync(
            "/webhooks/estimate-decisions",
            new ByteArrayContent(Encoding.UTF8.GetBytes("{}")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetails>(response);
        Assert.Equal((int)HttpStatusCode.Unauthorized, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Equal("Webhook signature is invalid or expired.", problem.Detail);
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn400_WhenSignedJsonIsMalformed()
    {
        using var client = _fixture.CreateClient();
        var body = Encoding.UTF8.GetBytes("{not-json");

        using var response = await SendSignedAsync(client, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetails>(response);
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("Validation error", problem.Title);
        Assert.Equal("Webhook payload is malformed.", problem.Detail);
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn404_WhenAggregateDoesNotExist()
    {
        using var client = _fixture.CreateClient();
        var body = Encoding.UTF8.GetBytes($$"""
            {"eventId":"{{Guid.NewGuid()}}","workOrderId":"{{Guid.NewGuid()}}","estimateId":"{{Guid.NewGuid()}}","decision":"Approved","occurredAt":"2026-07-11T12:00:00Z"}
            """);

        using var response = await SendSignedAsync(client, body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn401_WhenBodyExceedsHardLimit()
    {
        using var client = _fixture.CreateClient();
        var body = new byte[65_537];

        using var response = await SendSignedAsync(client, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task EstimateDecisionWebhook_ShouldReturn204_ForValidAnonymousDecision(string decision)
    {
        using var client = _fixture.CreateClientWithScope(out var scope);
        using (scope)
        {
            var (workOrderId, estimateId) = await SeedPendingEstimateAsync(scope);
            var body = CreateBody(Guid.NewGuid(), workOrderId, estimateId, decision);

            using var response = await SendSignedAsync(client, body);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn204_ForSameBodyReplay()
    {
        using var client = _fixture.CreateClientWithScope(out var scope);
        using (scope)
        {
            var (workOrderId, estimateId) = await SeedPendingEstimateAsync(scope);
            var body = CreateBody(Guid.NewGuid(), workOrderId, estimateId, "Approved");
            using var first = await SendSignedAsync(client, body);
            using var replay = await SendSignedAsync(client, body);

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        }
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn409_ForSameEventIdWithDifferentBody()
    {
        using var client = _fixture.CreateClientWithScope(out var scope);
        using (scope)
        {
            var (workOrderId, estimateId) = await SeedPendingEstimateAsync(scope);
            var eventId = Guid.NewGuid();
            using var first = await SendSignedAsync(client, CreateBody(eventId, workOrderId, estimateId, "Approved"));
            using var conflict = await SendSignedAsync(client, CreateBody(eventId, workOrderId, estimateId, "Rejected"));

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn401_WhenSignatureIsMalformed()
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/estimate-decisions")
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("{}"))
        };
        request.Headers.Add("X-GarageFlow-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        request.Headers.Add("X-GarageFlow-Signature", new string('A', 64));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EstimateDecisionWebhook_ShouldReturn401_WhenSignatureIsExpired()
    {
        using var client = _fixture.CreateClient();
        using var response = await SendSignedAsync(
            client,
            Encoding.UTF8.GetBytes("{}"),
            DateTimeOffset.UtcNow.AddSeconds(-301).ToUnixTimeSeconds());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendSignedAsync(
        HttpClient client,
        byte[] body,
        long? unixTimestamp = null)
    {
        var timestamp = (unixTimestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        var prefix = Encoding.ASCII.GetBytes(timestamp + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed, prefix.Length);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(
            IntegrationTestAuthSettings.EstimateDecisionWebhookSecret));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/estimate-decisions")
        {
            Content = new ByteArrayContent(body)
        };
        request.Headers.Add("X-GarageFlow-Timestamp", timestamp);
        request.Headers.Add("X-GarageFlow-Signature", Convert.ToHexStringLower(hmac.ComputeHash(signed)));

        return await client.SendAsync(request);
    }

    private static byte[] CreateBody(Guid eventId, Guid workOrderId, Guid estimateId, string decision) =>
        Encoding.UTF8.GetBytes($$"""
            {"eventId":"{{eventId}}","workOrderId":"{{workOrderId}}","estimateId":"{{estimateId}}","decision":"{{decision}}","occurredAt":"2026-07-11T12:00:00Z"}
            """);

    private static async Task<(Guid WorkOrderId, Guid EstimateId)> SeedPendingEstimateAsync(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();
        return (workOrder.Id.Value, workOrder.Estimates.Single().Id.Value);
    }
}
