using System.Net;
using System.Net.Http.Json;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Tests.Integration.Api.Auth.Contracts;
using GarageFlow.Tests.Integration.Api.Customers.Contracts;
using GarageFlow.Tests.Integration.Api.Services.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Services;
using GarageFlow.Tests.Shared.Users;

namespace GarageFlow.Tests.Integration.Api.Services;

public class ServicesApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task ServicesRoutes_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/services?page=1&pageSize=10");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActiveCustomer_ShouldReceive403_WhenListingServices()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var customerRequest = CustomerSeed.CreateUniqueBuilder()
            .WithFullName("Services Policy Customer")
            .WithEmail($"services.policy.{uniqueToken}@example.com")
            .WithPhoneNumber("11900030003")
            .BuildCreateRequest();

        var createCustomerResponse = await client.PostAsJsonAsync("/customers", customerRequest);
        HttpResponseAssertions.AssertStatus(createCustomerResponse, HttpStatusCode.Created);
        var createdCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(createCustomerResponse);

        var activatePortalResponse = await client.PostAsJsonAsync(
            $"/customers/{createdCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1992, 5, 16)));
        HttpResponseAssertions.AssertStatus(activatePortalResponse, HttpStatusCode.Created);
        var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activatePortalResponse);

        await AuthenticateCustomerPortalUserAsActiveAsync(
            client,
            customerUser.Email,
            customerUser.FullName,
            customerUser.BirthDate,
            "Customer.Policy.Services#123");

        var forbiddenResponse = await client.GetAsync("/services?page=1&pageSize=10");

        HttpResponseAssertions.AssertStatus(forbiddenResponse, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostService_ShouldReturn201_WhenRequestIsValid()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var request = new ServiceBuilder().BuildCreateRequest();

        var response = await client.PostAsJsonAsync("/services", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        Assert.NotNull(response.Headers.Location);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(request.Description, payload.Description);
        Assert.Equal(request.Price, payload.Price);
    }

    [Fact]
    public async Task GetServiceById_ShouldReturn404_WhenServiceDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/services/{Guid.NewGuid()}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetServiceById_ShouldReturn200_WhenServiceExists()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var serviceId = await CreateServiceIdAsync(
            client,
            new ServiceBuilder()
                .WithDescription("Diagnostic scanner")
                .WithPrice(149.90m));

        var response = await client.GetAsync($"/services/{serviceId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(response);
        Assert.Equal(serviceId, payload.Id);
        Assert.Equal("Diagnostic scanner", payload.Description);
        Assert.Equal(149.90m, payload.Price);
        Assert.NotEqual(default, payload.CreatedAt);
    }

    [Fact]
    public async Task ListServices_ShouldReturn200_WithPagination()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var firstServiceId = await CreateServiceIdAsync(
            client,
            new ServiceBuilder()
                .WithDescription("Brake cleaning")
                .WithPrice(69.90m));
        var secondServiceId = await CreateServiceIdAsync(
            client,
            new ServiceBuilder()
                .WithDescription("Suspension inspection")
                .WithPrice(89.90m));

        var response = await client.GetAsync("/services?page=1&pageSize=10");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ListServicesResponse>(response);
        Assert.Equal(1, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.True(payload.TotalCount >= 2);
        Assert.Contains(payload.Items, item => item.Id == firstServiceId);
        Assert.Contains(payload.Items, item => item.Id == secondServiceId);
    }

    [Fact]
    public async Task PutService_ShouldReturn200_WhenServiceExists()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var serviceId = await CreateServiceIdAsync(
            client,
            new ServiceBuilder()
                .WithDescription("Alignment")
                .WithPrice(89.90m));
        var request = new ServiceBuilder()
            .WithDescription("Alignment and balancing")
            .WithPrice(119.90m)
            .BuildUpdateRequest();

        var response = await client.PutAsJsonAsync($"/services/{serviceId}", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(response);
        Assert.Equal(serviceId, payload.Id);
        Assert.Equal("Alignment and balancing", payload.Description);
        Assert.Equal(119.90m, payload.Price);
    }

    [Fact]
    public async Task PutService_ShouldReturn404_WhenServiceDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var request = new ServiceBuilder()
            .WithDescription("Ghost service")
            .WithPrice(149.90m)
            .BuildUpdateRequest();

        var response = await client.PutAsJsonAsync($"/services/{Guid.NewGuid()}", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteService_ShouldReturn204_AndGetShouldReturn404_WhenServiceExists()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var serviceId = await CreateServiceIdAsync(
            client,
            new ServiceBuilder()
                .WithDescription("Tire rotation")
                .WithPrice(79.90m));

        var deleteResponse = await client.DeleteAsync($"/services/{serviceId}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/services/{serviceId}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CreateServiceIdAsync(
        HttpClient client,
        ServiceBuilder? builder = null)
    {
        var request = (builder ?? new ServiceBuilder()).BuildCreateRequest();
        var response = await client.PostAsJsonAsync("/services", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(response);
        return payload.Id;
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
