using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Api.Vehicles;

public class VehicleBrandsApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task GetVehicleBrandById_ShouldReturn404_WhenVehicleBrandDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/vehicle-brands/{Guid.NewGuid()}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VehicleBrandCrudRoutes_ShouldReturnExpectedStatuses()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new VehicleBrandBuilder()
                .WithName("Fiat")
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        Assert.NotNull(createResponse.Headers.Location);
        var created = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleBrandResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Fiat", created.Name);

        var listResponse = await client.GetAsync("/vehicle-brands?page=1&pageSize=10");
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleBrandResponse>>(listResponse);
        Assert.Equal(1, listPayload.Page);
        Assert.Equal(10, listPayload.PageSize);
        Assert.True(listPayload.TotalCount >= 1);

        var updateResponse = await client.PutAsJsonAsync(
            $"/vehicle-brands/{created.Id}",
            new VehicleBrandBuilder()
                .WithName("Fiat Updated")
                .BuildUpdateRequest());

        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);
        var updated = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleBrandResponse>(updateResponse);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Fiat Updated", updated.Name);

        var deleteResponse = await client.DeleteAsync($"/vehicle-brands/{created.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var getAfterDeleteResponse = await client.GetAsync($"/vehicle-brands/{created.Id}");
        HttpResponseAssertions.AssertStatus(getAfterDeleteResponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostVehicleBrand_ShouldReturn409_WhenVehicleBrandNameAlreadyExists()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var brandName = $"Brand-{Guid.NewGuid():N}";

        var firstCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new VehicleBrandBuilder()
                .WithName(brandName)
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(firstCreateResponse, HttpStatusCode.Created);

        var duplicateCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new VehicleBrandBuilder()
                .WithName($"  {brandName}  ")
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(duplicateCreateResponse, HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task PostVehicleBrand_ShouldReturn409_WhenVehicleBrandNameAlreadyExistsWithDifferentCase()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var brandName = $"Brand-{Guid.NewGuid():N}";

        var firstCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new VehicleBrandBuilder()
                .WithName(brandName.ToUpperInvariant())
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(firstCreateResponse, HttpStatusCode.Created);

        var duplicateCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new VehicleBrandBuilder()
                .WithName(brandName.ToLowerInvariant())
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(duplicateCreateResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteVehicleBrand_ShouldReturn409_WhenVehicleBrandHasRelatedVehicleModels()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var vehicleBrandId = await VehicleBrandSeed.CreateIdAsync(
            client,
            new VehicleBrandBuilder().WithName("Renault"));

        await VehicleModelSeed.CreateIdAsync(
            client,
            vehicleBrandId,
            new VehicleModelBuilder().WithName("Kwid"));

        var response = await client.DeleteAsync($"/vehicle-brands/{vehicleBrandId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Conflict);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(response);
        Assert.Equal("Business rule violation", payload.Title);
        Assert.Equal((int)HttpStatusCode.Conflict, payload.Status);
        Assert.Contains("cannot be deleted because it has related vehicle models.", payload.Detail);

        var getResponse = await client.GetAsync($"/vehicle-brands/{vehicleBrandId}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.OK);
        var vehicleBrand = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleBrandResponse>(getResponse);
        Assert.Equal(vehicleBrandId, vehicleBrand.Id);
    }
}

