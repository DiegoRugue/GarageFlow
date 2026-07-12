using System.Security.Cryptography;
using System.Text;
using GarageFlow.Adapters.Api.Webhooks.EstimateDecisions;
using Microsoft.Extensions.Options;

namespace GarageFlow.Tests.Integration.Api.Webhooks;

public sealed class EstimateDecisionWebhookSignatureValidatorTests
{
    private const string Secret = "integration-webhook-secret-32-characters-minimum";
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsValid_ShouldAcceptSignatureForExactRawBytes()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventId\":\"00000000-0000-0000-0000-000000000001\"}");
        var timestamp = Now.ToUnixTimeSeconds().ToString();

        var result = CreateValidator().IsValid(timestamp, Sign(timestamp, body), body);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_ShouldRejectOneByteBodyTamper()
    {
        var body = Encoding.UTF8.GetBytes("{\"decision\":\"Approved\"}");
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = Sign(timestamp, body);
        body[^3] = (byte)'x';

        Assert.False(CreateValidator().IsValid(timestamp, signature, body));
    }

    [Theory]
    [InlineData("{\"a\":1,\"b\":2}", "{ \"a\": 1, \"b\": 2 }")]
    [InlineData("{\"a\":1,\"b\":2}", "{\"b\":2,\"a\":1}")]
    public void IsValid_ShouldRejectSemanticallyEquivalentButByteDifferentBody(
        string signedJson,
        string receivedJson)
    {
        var signedBody = Encoding.UTF8.GetBytes(signedJson);
        var receivedBody = Encoding.UTF8.GetBytes(receivedJson);
        var timestamp = Now.ToUnixTimeSeconds().ToString();

        Assert.False(CreateValidator().IsValid(timestamp, Sign(timestamp, signedBody), receivedBody));
    }

    [Theory]
    [InlineData(null, "00")]
    [InlineData("1", null)]
    [InlineData("not-a-number", "00")]
    [InlineData("1", "abc")]
    [InlineData("1", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void IsValid_ShouldRejectMissingOrMalformedHeaders(string? timestamp, string? signature)
    {
        Assert.False(CreateValidator().IsValid(timestamp, signature, ReadOnlyMemory<byte>.Empty));
    }

    [Theory]
    [InlineData(-300, true)]
    [InlineData(300, true)]
    [InlineData(-301, false)]
    [InlineData(301, false)]
    public void IsValid_ShouldEnforceInclusiveClockSkewBoundary(int offsetSeconds, bool expected)
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = Now.AddSeconds(offsetSeconds).ToUnixTimeSeconds().ToString();

        Assert.Equal(expected, CreateValidator().IsValid(timestamp, Sign(timestamp, body), body));
    }

    [Theory]
    [InlineData(-300)]
    [InlineData(300)]
    public void IsValid_ShouldAcceptInclusiveClockSkewBoundary_WhenClockHasFractionalSeconds(
        int offsetSeconds)
    {
        var fractionalNow = Now.AddMilliseconds(900);
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = (fractionalNow.ToUnixTimeSeconds() + offsetSeconds).ToString();

        var result = CreateValidator(fractionalNow).IsValid(timestamp, Sign(timestamp, body), body);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_ShouldRejectBodyOverHardLimit()
    {
        var body = new byte[EstimateDecisionWebhookOptions.MaximumBodySizeBytes + 1];
        var timestamp = Now.ToUnixTimeSeconds().ToString();

        Assert.False(CreateValidator().IsValid(timestamp, new string('0', 64), body));
    }

    private static EstimateDecisionWebhookSignatureValidator CreateValidator() => CreateValidator(Now);

    private static EstimateDecisionWebhookSignatureValidator CreateValidator(DateTimeOffset now) =>
        new(
            Options.Create(new EstimateDecisionWebhookOptions { HmacSecret = Secret }),
            new FixedTimeProvider(now));

    private static string Sign(string timestamp, byte[] body)
    {
        var prefix = Encoding.ASCII.GetBytes(timestamp + ".");
        var signedBytes = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signedBytes, 0);
        body.CopyTo(signedBytes, prefix.Length);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return Convert.ToHexStringLower(hmac.ComputeHash(signedBytes));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
