using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Api.Vehicles;

public class VehicleColorsApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task GetVehicleColorById_ShouldReturn404_WhenVehicleColorDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/vehicle-colors/{Guid.NewGuid()}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VehicleColorCrudRoutes_ShouldReturnExpectedStatuses()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync(
            "/vehicle-colors",
            new VehicleColorBuilder()
                .WithName("Black")
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var created = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleColorResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Black", created.Name);

        var listResponse = await client.GetAsync("/vehicle-colors?page=1&pageSize=10");
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleColorResponse>>(listResponse);
        Assert.Equal(1, listPayload.Page);
        Assert.Equal(10, listPayload.PageSize);
        Assert.True(listPayload.TotalCount >= 1);

        var updateResponse = await client.PutAsJsonAsync(
            $"/vehicle-colors/{created.Id}",
            new VehicleColorBuilder()
                .WithName("Black Updated")
                .BuildUpdateRequest());

        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);
        var updated = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleColorResponse>(updateResponse);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Black Updated", updated.Name);

        var deleteResponse = await client.DeleteAsync($"/vehicle-colors/{created.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var getAfterDeleteResponse = await client.GetAsync($"/vehicle-colors/{created.Id}");
        HttpResponseAssertions.AssertStatus(getAfterDeleteResponse, HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task PostVehicleColor_ShouldReturn409_WhenVehicleColorNameAlreadyExistsWithDifferentCase()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var colorName = $"Color-{Guid.NewGuid():N}";

        var firstCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-colors",
            new VehicleColorBuilder()
                .WithName(colorName.ToUpperInvariant())
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(firstCreateResponse, HttpStatusCode.Created);

        var duplicateCreateResponse = await client.PostAsJsonAsync(
            "/vehicle-colors",
            new VehicleColorBuilder()
                .WithName(colorName.ToLowerInvariant())
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(duplicateCreateResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteVehicleColor_ShouldReturn409_WhenVehicleColorHasRelatedVehicles()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var seeded = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            colorBuilder: new VehicleColorBuilder().WithName("Silver"));

        var response = await client.DeleteAsync($"/vehicle-colors/{seeded.VehicleColorId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Conflict);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(response);
        Assert.Equal("Business rule violation", payload.Title);
        Assert.Equal((int)HttpStatusCode.Conflict, payload.Status);
        Assert.Contains("cannot be deleted because it has related vehicles.", payload.Detail);

        var getResponse = await client.GetAsync($"/vehicle-colors/{seeded.VehicleColorId}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.OK);
        var vehicleColor = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleColorResponse>(getResponse);
        Assert.Equal(seeded.VehicleColorId, vehicleColor.Id);
    }
}

