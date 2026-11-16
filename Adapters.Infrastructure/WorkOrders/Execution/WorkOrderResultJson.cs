using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;

internal static class WorkOrderResultJson
{
    private static readonly string[] TimestampFormats = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    public static bool TryReadTimestamp(string value, out DateTimeOffset occurredAt)
    {
        occurredAt = default;
        return (value.EndsWith('Z') || (value.Length >= 25 && value[^6] is '+' or '-'))
            && DateTimeOffset.TryParseExact(value, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out occurredAt);
    }

    public static Guid ReadId(string value)
    {
        if (!Guid.TryParseExact(value, "D", out var id)
            || id == Guid.Empty
            || !string.Equals(value, id.ToString("D"), StringComparison.Ordinal))
        {
            throw new JsonException("Expected a non-empty canonical UUID.");
        }

        return id;
    }
}
