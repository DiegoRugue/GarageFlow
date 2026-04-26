using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Api.Vehicles;

public class VehicleModelsApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task GetVehicleModelById_ShouldReturn404_WhenVehicleModelDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/vehicle-models/{Guid.NewGuid()}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VehicleModelCrudRoutes_ShouldReturnExpectedStatuses()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var vehicleBrandId = await VehicleBrandSeed.CreateIdAsync(
            client,
            new VehicleBrandBuilder().WithName("Ford"));

        var createResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new VehicleModelBuilder()
                .WithVehicleBrandId(vehicleBrandId)
                .WithName("Focus")
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var created = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleModelResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(vehicleBrandId, created.VehicleBrandId);
        Assert.Equal("Focus", created.Name);

        var listResponse = await client.GetAsync("/vehicle-models?page=1&pageSize=10");
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleModelResponse>>(listResponse);
        Assert.Equal(1, listPayload.Page);
        Assert.Equal(10, listPayload.PageSize);
        Assert.True(listPayload.TotalCount >= 1);

        var updateResponse = await client.PutAsJsonAsync(
            $"/vehicle-models/{created.Id}",
            new VehicleModelBuilder()
                .WithVehicleBrandId(vehicleBrandId)
                .WithName("Focus Updated")
                .BuildUpdateRequest());

        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);
        var updated = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleModelResponse>(updateResponse);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(vehicleBrandId, updated.VehicleBrandId);
        Assert.Equal("Focus Updated", updated.Name);

        var deleteResponse = await client.DeleteAsync($"/vehicle-models/{created.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var getAfterDeleteResponse = await client.GetAsync($"/vehicle-models/{created.Id}");
        HttpResponseAssertions.AssertStatus(getAfterDeleteResponse, HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task PostVehicleModel_ShouldReturn409_WhenVehicleModelNameAlreadyExistsWithDifferentCaseInSameBrand()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var vehicleBrandId = await VehicleBrandSeed.CreateIdAsync(
            client,
            new VehicleBrandBuilder().WithName($"Brand-{Guid.NewGuid():N}"));

        var modelName = $"Model-{Guid.NewGuid():N}";

        var firstCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new VehicleModelBuilder()
                .WithVehicleBrandId(vehicleBrandId)
                .WithName(modelName.ToUpperInvariant())
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(firstCreateResponse, HttpStatusCode.Created);

        var duplicateCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new VehicleModelBuilder()
                .WithVehicleBrandId(vehicleBrandId)
                .WithName(modelName.ToLowerInvariant())
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(duplicateCreateResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteVehicleModel_ShouldReturn409_WhenVehicleModelHasRelatedVehicles()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var seeded = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            modelBuilder: new VehicleModelBuilder().WithName("HB20"));

        var response = await client.DeleteAsync($"/vehicle-models/{seeded.VehicleModelId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Conflict);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(response);
        Assert.Equal("Business rule violation", payload.Title);
        Assert.Equal((int)HttpStatusCode.Conflict, payload.Status);
        Assert.Contains("cannot be deleted because it has related vehicles.", payload.Detail);

        var getResponse = await client.GetAsync($"/vehicle-models/{seeded.VehicleModelId}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.OK);
        var vehicleModel = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleModelResponse>(getResponse);
        Assert.Equal(seeded.VehicleModelId, vehicleModel.Id);
    }

    [Fact]
    public async Task ListVehicleModels_ShouldFilterByVehicleBrandId()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var targetBrandId = await VehicleBrandSeed.CreateIdAsync(
            client,
            new VehicleBrandBuilder().WithName($"Brand-{Guid.NewGuid():N}"));

        var otherBrandId = await VehicleBrandSeed.CreateIdAsync(
            client,
            new VehicleBrandBuilder().WithName($"Brand-{Guid.NewGuid():N}"));

        var createTargetModelResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new VehicleModelBuilder()
                .WithVehicleBrandId(targetBrandId)
                .WithName("Target")
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(createTargetModelResponse, HttpStatusCode.Created);
        var createdTargetModel = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleModelResponse>(createTargetModelResponse);

        var createOtherModelResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new VehicleModelBuilder()
                .WithVehicleBrandId(otherBrandId)
                .WithName("Other")
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(createOtherModelResponse, HttpStatusCode.Created);

        var response = await client.GetAsync($"/vehicle-models?page=1&pageSize=20&vehicleBrandId={targetBrandId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleModelResponse>>(response);

        Assert.All(payload.Items, item => Assert.Equal(targetBrandId, item.VehicleBrandId));
        Assert.Contains(payload.Items, item => item.Id == createdTargetModel.Id);
        Assert.DoesNotContain(payload.Items, item => item.VehicleBrandId == otherBrandId);
    }
}

