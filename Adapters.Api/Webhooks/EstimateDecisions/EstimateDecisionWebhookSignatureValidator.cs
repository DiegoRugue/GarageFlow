using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Api.Webhooks.EstimateDecisions;

public sealed class EstimateDecisionWebhookSignatureValidator(
    IOptions<EstimateDecisionWebhookOptions> options,
    TimeProvider timeProvider)
{
    private const int SignatureHexLength = 64;
    private readonly EstimateDecisionWebhookOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _timeProvider = timeProvider
        ?? throw new ArgumentNullException(nameof(timeProvider));

    public bool IsValid(
        string? timestampHeader,
        string? signatureHeader,
        ReadOnlyMemory<byte> rawBody)
    {
        if (rawBody.Length > EstimateDecisionWebhookOptions.MaximumBodySizeBytes
            || !long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || !IsLowercaseHexSignature(signatureHeader))
        {
            return false;
        }

        var nowTimestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (timestamp < nowTimestamp - EstimateDecisionWebhookOptions.AllowedClockSkewSeconds
            || timestamp > nowTimestamp + EstimateDecisionWebhookOptions.AllowedClockSkewSeconds)
        {
            return false;
        }

        var prefix = Encoding.ASCII.GetBytes(timestampHeader + ".");
        var signedBytes = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(signedBytes, 0);
        rawBody.Span.CopyTo(signedBytes.AsSpan(prefix.Length));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.HmacSecret));
        var expected = hmac.ComputeHash(signedBytes);
        var supplied = Convert.FromHexString(signatureHeader!);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static bool IsLowercaseHexSignature(string? signature) =>
        signature is { Length: SignatureHexLength }
        && signature.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
