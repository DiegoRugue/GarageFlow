using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Tests.Integration.Api.Customers.Contracts;
using GarageFlow.Tests.Integration.Api.InventoryItems.Contracts;
using GarageFlow.Tests.Integration.Api.Users.Contracts;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.InventoryItems;
using GarageFlow.Tests.Shared.Users;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Api.WorkOrders;

public class WorkOrdersApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task WorkOrdersStaffRoutes_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/work-orders?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActiveStaff_ShouldCreateListAndGetWorkOrders()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder(),
            builder: new VehicleBuilder().WithPlate($"WKR{DateTime.UtcNow.Ticks % 10000:D4}"));

        var createResponse = await client.PostAsJsonAsync(
            "/work-orders",
            new CreateWorkOrderRequest(seededVehicle.CustomerId, seededVehicle.VehicleId));

        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        Assert.NotNull(createResponse.Headers.Location);
        var created = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Created", created.Status);
        Assert.Equal(seededVehicle.CustomerId, created.CustomerId);
        Assert.Equal(seededVehicle.VehicleId, created.VehicleId);

        var listResponse = await client.GetAsync($"/work-orders?page=1&pageSize=20&customerId={seededVehicle.CustomerId}");

        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<WorkOrderDetailsResponse>>(listResponse);
        Assert.Contains(listPayload.Items, item => item.Id == created.Id);

        var getResponse = await client.GetAsync($"/work-orders/{created.Id}");

        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.OK);
        var details = await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(getResponse);
        Assert.Equal(created.Id, details.Id);
        Assert.Equal(seededVehicle.CustomerId, details.CustomerId);
        Assert.Equal(seededVehicle.VehicleId, details.VehicleId);
        Assert.Equal("Created", details.Status);
    }

    [Fact]
    public async Task ActiveCustomer_ShouldAccessOwnMeWorkOrders_AndBeForbiddenFromStaffRoute()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder(),
            builder: new VehicleBuilder().WithPlate($"MEW{DateTime.UtcNow.Ticks % 10000:D4}"));

        var createResponse = await staffClient.PostAsJsonAsync(
            "/work-orders",
            new CreateWorkOrderRequest(seededVehicle.CustomerId, seededVehicle.VehicleId));
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var created = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderResponse>(createResponse);

        var activateResponse = await staffClient.PostAsJsonAsync(
            $"/customers/{seededVehicle.CustomerId}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1991, 1, 11)));
        HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
        var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

        using var customerClient = _fixture.CreateClient();
        await AuthenticateCreatedUserAsActiveAsync(
            customerClient,
            customerUser.Email,
            customerUser.FullName,
            customerUser.BirthDate,
            "Customer.WorkOrders.Active#123");

        var myListResponse = await customerClient.GetAsync("/me/work-orders?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(myListResponse, HttpStatusCode.OK);
        var myListPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<WorkOrderDetailsResponse>>(myListResponse);
        Assert.Contains(myListPayload.Items, item => item.Id == created.Id);

        var staffRouteResponse = await customerClient.GetAsync("/work-orders?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(staffRouteResponse, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CustomerWorkOrderDetails_ShouldNotExposeUnitCost_WhenInventoryLinesExist()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder(),
            builder: new VehicleBuilder().WithPlate($"CST{DateTime.UtcNow.Ticks % 10000:D4}"));

        var createdWorkOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var createdEstimate = await CreateEstimateAsync(staffClient, createdWorkOrder.Id);
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName("Timing Belt")
                .WithDescription("Timing belt replacement part")
                .WithCost(120m)
                .WithPrice(220m)
                .WithStockQuantity(30));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 1));
        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        var activateResponse = await staffClient.PostAsJsonAsync(
            $"/customers/{seededVehicle.CustomerId}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1990, 10, 20)));
        HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
        var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

        using var customerClient = _fixture.CreateClient();
        await AuthenticateCreatedUserAsActiveAsync(
            customerClient,
            customerUser.Email,
            customerUser.FullName,
            customerUser.BirthDate,
            "Customer.UnitCost.Active#123");

        var response = await customerClient.GetAsync($"/me/work-orders/{createdWorkOrder.Id}");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);

        await using var bodyStream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(bodyStream);
        var inventoryLine = json.RootElement
            .GetProperty("estimates")[0]
            .GetProperty("inventoryLines")[0];

        Assert.True(inventoryLine.TryGetProperty("unitPrice", out _));
        Assert.True(inventoryLine.TryGetProperty("totalPrice", out _));
        Assert.False(inventoryLine.TryGetProperty("unitCost", out _));
    }

    private static async Task<HttpClient> CreateAuthenticatedAttendantClientAsync(GarageFlowApiFixture fixture)
    {
        var client = await fixture.CreateAuthenticatedClientAsync();

        const string activePassword = "WorkOrders.Attendant.Active#123";
        var uniqueToken = Guid.NewGuid().ToString("N");
        var fullName = $"WorkOrders Attendant {uniqueToken}";
        var birthDate = new DateOnly(1994, 4, 8);

        var createRequest = new UserBuilder()
            .WithFullName(fullName)
            .WithEmail($"workorders.attendant.{uniqueToken}@example.com")
            .WithBirthDate(birthDate)
            .WithRole(UserRole.Attendant)
            .BuildCreateRequest();

        var createUserResponse = await client.PostAsJsonAsync("/users", createRequest);
        HttpResponseAssertions.AssertStatus(createUserResponse, HttpStatusCode.Created);
        var createdUser = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createUserResponse);

        var initialPassword = User.GenerateInitialPassword(FullName.Create(fullName), birthDate);
        var firstLogin = await client.LoginAndAttachBearerTokenAsync(createdUser.Email, initialPassword);
        Assert.True(firstLogin.MustChangePassword);

        var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: initialPassword,
                NewPassword: activePassword));
        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);

        var activeLogin = await client.LoginAndAttachBearerTokenAsync(createdUser.Email, activePassword);
        Assert.False(activeLogin.MustChangePassword);

        return client;
    }

    private static async Task AuthenticateCreatedUserAsActiveAsync(
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
    }

    private static async Task<CreateWorkOrderResponse> CreateWorkOrderAsync(HttpClient client, Guid customerId, Guid vehicleId)
    {
        var response = await client.PostAsJsonAsync("/work-orders", new CreateWorkOrderRequest(customerId, vehicleId));
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderResponse>(response);
    }

    private static async Task<CreateEstimateResponse> CreateEstimateAsync(HttpClient client, Guid workOrderId)
    {
        var response = await client.PostAsJsonAsync($"/work-orders/{workOrderId}/estimates", new { });
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<CreateEstimateResponse>(response);
    }

    private static async Task<InventoryItemResponse> CreateInventoryItemAsync(HttpClient client, InventoryItemBuilder? builder = null)
    {
        var request = (builder ?? new InventoryItemBuilder()).BuildCreateRequest();
        var response = await client.PostAsJsonAsync("/inventory-items", request);
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(response);
    }

    private sealed record CreateWorkOrderRequest(Guid CustomerId, Guid VehicleId);

    private sealed record CreateWorkOrderResponse(
        Guid Id,
        Guid CustomerId,
        Guid VehicleId,
        string Status,
        DateTime CreatedAt);

    private sealed record CreateEstimateResponse(
        Guid Id,
        Guid WorkOrderId,
        string Status,
        decimal TotalAmount,
        DateTime CreatedAt);

    private sealed record AddEstimateInventoryItemRequest(Guid InventoryItemId, int Quantity);

    private sealed record WorkOrderDetailsResponse(
        Guid Id,
        Guid CustomerId,
        Guid VehicleId,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
