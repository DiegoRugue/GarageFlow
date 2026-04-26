using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Support.Seed;

public static class VehicleColorSeed
{
    public static async Task<Guid> CreateIdAsync(
        HttpClient client,
        VehicleColorBuilder? builder = null)
    {
        var effectiveBuilder = builder ?? new VehicleColorBuilder().WithName($"Color-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync(
            "/vehicle-colors",
            effectiveBuilder.BuildCreateRequest());

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VehicleColorResponse>();
        if (payload is null)
        {
            throw new InvalidOperationException("Create vehicle color response could not be deserialized.");
        }

        return payload.Id;
    }
}
