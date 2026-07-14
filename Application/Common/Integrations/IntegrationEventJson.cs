using System.Text.Json;

namespace GarageFlow.Application.Common.Integrations;

public static class IntegrationEventJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, SerializerOptions);

    public static TIntegrationEvent? Deserialize<TIntegrationEvent>(string payload) =>
        JsonSerializer.Deserialize<TIntegrationEvent>(payload, SerializerOptions);
}
