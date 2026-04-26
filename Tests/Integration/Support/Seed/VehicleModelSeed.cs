using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Support.Seed;

public static class VehicleModelSeed
{
    public static async Task<Guid> CreateIdAsync(
        HttpClient client,
        Guid vehicleBrandId,
        VehicleModelBuilder? builder = null)
    {
        var effectiveBuilder = builder ?? new VehicleModelBuilder().WithName($"Model-{Guid.NewGuid():N}");
        effectiveBuilder.WithVehicleBrandId(vehicleBrandId);

        var response = await client.PostAsJsonAsync(
            "/vehicle-models",
            effectiveBuilder.BuildCreateRequest());

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VehicleModelResponse>();
        if (payload is null)
        {
            throw new InvalidOperationException("Create vehicle model response could not be deserialized.");
        }

        return payload.Id;
    }
}
