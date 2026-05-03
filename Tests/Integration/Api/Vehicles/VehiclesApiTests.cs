using System.Net;
using System.Net.Http.Json;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Tests.Integration.Api.Auth.Contracts;
using GarageFlow.Tests.Integration.Api.Customers.Contracts;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Users;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Api.Vehicles;

public class VehiclesApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task VehiclesRoutes_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/vehicles?page=1&pageSize=10");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActiveCustomer_ShouldReceive403_WhenListingVehicles()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var customerRequest = CustomerSeed.CreateUniqueBuilder()
            .WithFullName("Vehicles Policy Customer")
            .WithEmail($"vehicles.policy.{uniqueToken}@example.com")
            .WithPhoneNumber("11900020002")
            .BuildCreateRequest();

        var createCustomerResponse = await client.PostAsJsonAsync("/customers", customerRequest);
        HttpResponseAssertions.AssertStatus(createCustomerResponse, HttpStatusCode.Created);
        var createdCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(createCustomerResponse);

        var activatePortalResponse = await client.PostAsJsonAsync(
            $"/customers/{createdCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1991, 4, 15)));
        HttpResponseAssertions.AssertStatus(activatePortalResponse, HttpStatusCode.Created);
        var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activatePortalResponse);

        await AuthenticateCustomerPortalUserAsActiveAsync(
            client,
            customerUser.Email,
            customerUser.FullName,
            customerUser.BirthDate,
            "Customer.Policy.Vehicles#123");

        var forbiddenResponse = await client.GetAsync("/vehicles?page=1&pageSize=10");

        HttpResponseAssertions.AssertStatus(forbiddenResponse, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetVehicleById_ShouldReturn404_WhenVehicleDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/vehicles/{Guid.NewGuid()}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VehicleCrudRoutes_ShouldReturnExpectedStatuses()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var brandName = "Chevrolet";
        var modelName = "Onix";
        var colorName = "White";

        var dependencies = await VehicleSeed.CreateDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder(),
            brandBuilder: new VehicleBrandBuilder().WithName(brandName),
            modelBuilder: new VehicleModelBuilder().WithName(modelName),
            colorBuilder: new VehicleColorBuilder().WithName(colorName));

        var createResponse = await client.PostAsJsonAsync(
            "/vehicles",
            new VehicleBuilder()
                .WithPlate("ABC1234")
                .WithYear(2024)
                .WithDependencies(
                    dependencies.CustomerId,
                    dependencies.VehicleBrandId,
                    dependencies.VehicleModelId,
                    dependencies.VehicleColorId)
                .BuildCreateRequest());

        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var created = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(dependencies.CustomerId, created.CustomerId);
        Assert.Equal(dependencies.VehicleBrandId, created.VehicleBrandId);
        Assert.Equal(dependencies.VehicleModelId, created.VehicleModelId);
        Assert.Equal(dependencies.VehicleColorId, created.VehicleColorId);
        Assert.Equal("ABC1234", created.Plate);
        var getResponse = await client.GetAsync($"/vehicles/{created.Id}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.OK);
        var getById = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleResponse>(getResponse);
        Assert.Equal(created.Id, getById.Id);
        Assert.Equal(brandName, getById.VehicleBrandName);
        Assert.Equal(modelName, getById.VehicleModelName);
        Assert.Equal(colorName, getById.VehicleColorName);

        var listResponse = await client.GetAsync($"/vehicles?page=1&pageSize=10&customerId={dependencies.CustomerId}");
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleResponse>>(listResponse);
        Assert.Equal(1, listPayload.Page);
        Assert.Equal(10, listPayload.PageSize);
        Assert.True(listPayload.TotalCount >= 1);
        Assert.All(listPayload.Items, item => Assert.Equal(dependencies.CustomerId, item.CustomerId));
        Assert.Contains(
            listPayload.Items,
            item => item.Id == created.Id &&
                    item.VehicleBrandName == brandName &&
                    item.VehicleModelName == modelName &&
                    item.VehicleColorName == colorName);

        var updateResponse = await client.PutAsJsonAsync(
            $"/vehicles/{created.Id}",
            new VehicleBuilder()
                .WithPlate("XYZ1A23")
                .WithYear(2025)
                .WithDependencies(
                    dependencies.CustomerId,
                    dependencies.VehicleBrandId,
                    dependencies.VehicleModelId,
                    dependencies.VehicleColorId)
                .BuildUpdateRequest());

        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);
        var updated = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleResponse>(updateResponse);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(2025, updated.Year);
        Assert.Equal("XYZ1A23", updated.Plate);

        var deleteResponse = await client.DeleteAsync($"/vehicles/{created.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var getAfterDeleteResponse = await client.GetAsync($"/vehicles/{created.Id}");
        HttpResponseAssertions.AssertStatus(getAfterDeleteResponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVehicles_ShouldFilterByCustomerId()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var firstCustomerVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            builder: new VehicleBuilder().WithPlate("AAA1A01"),
            customerBuilder: CustomerSeed.CreateUniqueBuilder().WithFullName("Customer A"),
            brandBuilder: new VehicleBrandBuilder().WithName($"Ford-{Guid.NewGuid():N}"),
            modelBuilder: new VehicleModelBuilder().WithName($"Ka-{Guid.NewGuid():N}"),
            colorBuilder: new VehicleColorBuilder().WithName($"Red-{Guid.NewGuid():N}"));

        var secondCustomerVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            builder: new VehicleBuilder().WithPlate("BBB1B02"),
            customerBuilder: CustomerSeed.CreateUniqueBuilder().WithFullName("Customer B"),
            brandBuilder: new VehicleBrandBuilder().WithName($"Fiat-{Guid.NewGuid():N}"),
            modelBuilder: new VehicleModelBuilder().WithName($"Palio-{Guid.NewGuid():N}"),
            colorBuilder: new VehicleColorBuilder().WithName($"Blue-{Guid.NewGuid():N}"));

        var filterResponse = await client.GetAsync(
            $"/vehicles?page=1&pageSize=10&customerId={firstCustomerVehicle.CustomerId}");
        HttpResponseAssertions.AssertStatus(filterResponse, HttpStatusCode.OK);
        var filterPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleResponse>>(filterResponse);

        Assert.True(filterPayload.Items.Count >= 1);
        Assert.All(filterPayload.Items, vehicle => Assert.Equal(firstCustomerVehicle.CustomerId, vehicle.CustomerId));
        Assert.Contains(
            filterPayload.Items,
            vehicle => vehicle.Id == firstCustomerVehicle.VehicleId &&
                       vehicle.Plate == "AAA1A01");
        Assert.DoesNotContain(
            filterPayload.Items,
            vehicle => vehicle.Id == secondCustomerVehicle.VehicleId);
    }

    private static async Task<LoginResponse> AuthenticateCustomerPortalUserAsActiveAsync(
        HttpClient client,
        string email,
        string fullName,
        DateOnly birthDate,
        string newPassword)
    {
        var initialPassword = User.GenerateInitialPassword(FullName.Create(fullName), birthDate);
        var firstLogin = await client.LoginAndAttachBearerTokenAsync(email, initialPassword);
        Assert.True(firstLogin.MustChangePassword);

        var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: initialPassword,
                NewPassword: newPassword));
        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);

        var activeLogin = await client.LoginAndAttachBearerTokenAsync(email, newPassword);
        Assert.False(activeLogin.MustChangePassword);
        return activeLogin;
    }
}
