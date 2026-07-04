using System.Net;
using System.Net.Http.Json;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Tests.Integration.Api.InventoryItems.Contracts;
using GarageFlow.Tests.Integration.Api.Users.Contracts;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Shared.InventoryItems;
using GarageFlow.Tests.Shared.Users;

namespace GarageFlow.Tests.Integration.Api.InventoryItems;

public class InventoryItemsApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task InventoryItemsRoutes_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/inventory-items?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InventoryItemsRoutes_ShouldReturn403_ForAdminToken()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/inventory-items?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Attendant_ShouldCreateInventoryItem_WhenRequestIsValid()
    {
        using var client = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var createRequest = new InventoryItemBuilder()
            .WithName("Brake Pad")
            .WithDescription("Ceramic brake pad for front axle")
            .WithCost(19.90m)
            .WithPrice(49.90m)
            .WithStockQuantity(100)
            .BuildCreateRequest();

        var response = await client.PostAsJsonAsync("/inventory-items", createRequest);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(createRequest.Name, payload.Name);
        Assert.Equal(createRequest.Description, payload.Description);
        Assert.Equal(createRequest.Type, payload.Type);
        Assert.Equal(createRequest.Cost, payload.Cost);
        Assert.Equal(createRequest.Price, payload.Price);
        Assert.Equal(createRequest.StockQuantity, payload.StockQuantity);
    }

    [Fact]
    public async Task Attendant_ShouldGetInventoryItemById_WhenItemExists()
    {
        using var client = await CreateAuthenticatedAttendantClientAsync(_fixture);
        var created = await CreateInventoryItemAsync(
            client,
            new InventoryItemBuilder()
                .WithName("Spark Plug")
                .WithDescription("Spark plug for 4-cylinder engine")
                .WithStockQuantity(55));

        var response = await client.GetAsync($"/inventory-items/{created.Id}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(response);
        Assert.Equal(created.Id, payload.Id);
        Assert.Equal(created.Name, payload.Name);
        Assert.Equal(created.StockQuantity, payload.StockQuantity);
    }

    [Fact]
    public async Task Attendant_ShouldListInventoryItems_WhenItemsExist()
    {
        using var client = await CreateAuthenticatedAttendantClientAsync(_fixture);
        var created = await CreateInventoryItemAsync(client, new InventoryItemBuilder().WithName("Coolant"));

        var response = await client.GetAsync("/inventory-items?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<InventoryItemResponse>>(response);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.True(payload.TotalCount >= 1);
        Assert.Contains(
            payload.Items,
            item => item.Id == created.Id &&
                    item.Name == created.Name);
    }

    [Fact]
    public async Task Attendant_ShouldUpdateInventoryItem_WhenRequestIsValid()
    {
        using var client = await CreateAuthenticatedAttendantClientAsync(_fixture);
        var created = await CreateInventoryItemAsync(
            client,
            new InventoryItemBuilder()
                .WithName("Oil")
                .WithDescription("Synthetic oil 5W30")
                .WithCost(25.00m)
                .WithPrice(59.90m)
                .WithStockQuantity(10));

        var updateRequest = new InventoryItemBuilder()
            .WithName("Synthetic Oil")
            .WithDescription("Synthetic 5W30 with long-life")
            .WithCost(35.00m)
            .WithPrice(79.90m)
            .BuildUpdateRequest();

        var response = await client.PutAsJsonAsync($"/inventory-items/{created.Id}", updateRequest);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(response);
        Assert.Equal(created.Id, payload.Id);
        Assert.Equal(updateRequest.Name, payload.Name);
        Assert.Equal(updateRequest.Description, payload.Description);
        Assert.Equal(updateRequest.Type, payload.Type);
        Assert.Equal(updateRequest.Cost, payload.Cost);
        Assert.Equal(updateRequest.Price, payload.Price);
    }

    [Fact]
    public async Task Attendant_ShouldDeleteInventoryItem_WhenItemExists()
    {
        using var client = await CreateAuthenticatedAttendantClientAsync(_fixture);
        var created = await CreateInventoryItemAsync(
            client,
            new InventoryItemBuilder()
                .WithName("Wiper Blade")
                .WithDescription("Front wiper")
                .WithStockQuantity(20));

        var deleteResponse = await client.DeleteAsync($"/inventory-items/{created.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/inventory-items/{created.Id}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Attendant_ShouldUpdateStock_WhenPatchPayloadIsValid()
    {
        using var client = await CreateAuthenticatedAttendantClientAsync(_fixture);
        var created = await CreateInventoryItemAsync(
            client,
            new InventoryItemBuilder()
                .WithName("Brake Fluid")
                .WithDescription("DOT 4 brake fluid")
                .WithStockQuantity(12));

        var updateStockRequest = new InventoryItemBuilder()
            .WithStockQuantity(30)
            .BuildUpdateStockRequest();

        var response = await client.PatchAsJsonAsync($"/inventory-items/{created.Id}/stock", updateStockRequest);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(response);
        Assert.Equal(created.Id, payload.Id);
        Assert.Equal(updateStockRequest.StockQuantity, payload.StockQuantity);
    }

    [Fact]
    public async Task Attendant_ShouldReturn400_WhenUpdatingStockToNegative()
    {
        using var client = await CreateAuthenticatedAttendantClientAsync(_fixture);
        var created = await CreateInventoryItemAsync(
            client,
            new InventoryItemBuilder().WithName("Fuel Filter").WithStockQuantity(10));

        var invalidUpdateRequest = new InventoryItemBuilder().WithStockQuantity(-1).BuildUpdateStockRequest();

        var response = await client.PatchAsJsonAsync($"/inventory-items/{created.Id}/stock", invalidUpdateRequest);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.BadRequest);
    }

    private static async Task<HttpClient> CreateAuthenticatedAttendantClientAsync(GarageFlowApiFixture fixture)
    {
        var client = await fixture.CreateAuthenticatedClientAsync();

        const string activePassword = "Attendant.Active#123";
        var uniqueToken = Guid.NewGuid().ToString("N");
        var fullName = $"Integration Attendant {uniqueToken}";
        var birthDate = new DateOnly(1994, 4, 8);

        var createRequest = new UserBuilder()
            .WithFullName(fullName)
            .WithEmail($"integration.attendant.{uniqueToken}@example.com")
            .WithBirthDate(birthDate)
            .WithRole(UserRole.Attendant)
            .BuildCreateRequest();

        var createUserResponse = await client.PostAsJsonAsync("/users", createRequest);
        HttpResponseAssertions.AssertStatus(createUserResponse, HttpStatusCode.Created);
        var createdUser = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createUserResponse);

        var initialPassword = User.GenerateInitialPassword(FullName.Create(fullName), birthDate);
        var firstLogin = await client.LoginAndAttachBearerTokenAsync(
            createdUser.Email,
            initialPassword);
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

    private static async Task<InventoryItemResponse> CreateInventoryItemAsync(
        HttpClient client,
        InventoryItemBuilder? builder = null)
    {
        var request = (builder ?? new InventoryItemBuilder()).BuildCreateRequest();

        var response = await client.PostAsJsonAsync("/inventory-items", request);
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        return await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(response);
    }
}
