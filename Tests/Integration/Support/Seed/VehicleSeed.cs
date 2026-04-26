using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Support.Seed;

public static class VehicleSeed
{
    private static int _licensePlateSequence;

    public static async Task<Guid> CreateIdAsync(
        HttpClient client,
        VehicleBuilder? builder = null,
        CustomerBuilder? customerBuilder = null,
        VehicleBrandBuilder? brandBuilder = null,
        VehicleModelBuilder? modelBuilder = null,
        VehicleColorBuilder? colorBuilder = null)
    {
        var seeded = await CreateWithDependenciesAsync(
            client,
            builder,
            customerBuilder,
            brandBuilder,
            modelBuilder,
            colorBuilder);

        return seeded.VehicleId;
    }

    public static async Task<VehicleDependencySeedResult> CreateDependenciesAsync(
        HttpClient client,
        CustomerBuilder? customerBuilder = null,
        VehicleBrandBuilder? brandBuilder = null,
        VehicleModelBuilder? modelBuilder = null,
        VehicleColorBuilder? colorBuilder = null)
    {
        var vehicleBrandId = await VehicleBrandSeed.CreateIdAsync(client, brandBuilder);
        var vehicleModelId = await VehicleModelSeed.CreateIdAsync(client, vehicleBrandId, modelBuilder);
        var vehicleColorId = await VehicleColorSeed.CreateIdAsync(client, colorBuilder);
        var customerId = await CustomerSeed.CreateIdAsync(client, customerBuilder);

        return new VehicleDependencySeedResult(
            CustomerId: customerId,
            VehicleBrandId: vehicleBrandId,
            VehicleModelId: vehicleModelId,
            VehicleColorId: vehicleColorId);
    }

    public static async Task<VehicleSeedResult> CreateWithDependenciesAsync(
        HttpClient client,
        VehicleBuilder? builder = null,
        CustomerBuilder? customerBuilder = null,
        VehicleBrandBuilder? brandBuilder = null,
        VehicleModelBuilder? modelBuilder = null,
        VehicleColorBuilder? colorBuilder = null)
    {
        var dependencies = await CreateDependenciesAsync(
            client,
            customerBuilder,
            brandBuilder,
            modelBuilder,
            colorBuilder);

        var effectiveBuilder = builder ?? new VehicleBuilder().WithPlate(NextLicensePlate());
        effectiveBuilder.WithDependencies(
            dependencies.CustomerId,
            dependencies.VehicleBrandId,
            dependencies.VehicleModelId,
            dependencies.VehicleColorId);

        var response = await client.PostAsJsonAsync(
            "/vehicles",
            effectiveBuilder.BuildCreateRequest());

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VehicleResponse>();
        if (payload is null)
        {
            throw new InvalidOperationException("Create vehicle response could not be deserialized.");
        }

        return new VehicleSeedResult(
            VehicleId: payload.Id,
            CustomerId: dependencies.CustomerId,
            VehicleBrandId: dependencies.VehicleBrandId,
            VehicleModelId: dependencies.VehicleModelId,
            VehicleColorId: dependencies.VehicleColorId);
    }

    private static string NextLicensePlate()
    {
        return $"TSD{Interlocked.Increment(ref _licensePlateSequence):D4}";
    }
}

public sealed record VehicleDependencySeedResult(
    Guid CustomerId,
    Guid VehicleBrandId,
    Guid VehicleModelId,
    Guid VehicleColorId);

public sealed record VehicleSeedResult(
    Guid VehicleId,
    Guid CustomerId,
    Guid VehicleBrandId,
    Guid VehicleModelId,
    Guid VehicleColorId);
