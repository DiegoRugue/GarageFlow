using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Support.Seed;

public static class VehicleBrandSeed
{
    public static async Task<Guid> CreateIdAsync(
        HttpClient client,
        VehicleBrandBuilder? builder = null)
    {
        var effectiveBuilder = builder ?? new VehicleBrandBuilder().WithName($"Brand-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync(
            "/vehicle-brands",
            effectiveBuilder.BuildCreateRequest());

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VehicleBrandResponse>();
        if (payload is null)
        {
            throw new InvalidOperationException("Create vehicle brand response could not be deserialized.");
        }

        return payload.Id;
    }
}
