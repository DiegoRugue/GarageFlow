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
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.Users;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Api.Customers;

public class CustomersApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task CustomersRoutes_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/customers?page=1&pageSize=10");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActiveCustomer_ShouldReceive403_WhenListingCustomers()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var customerRequest = CustomerSeed.CreateUniqueBuilder()
            .WithFullName("Customers Policy Customer")
            .WithEmail($"customers.policy.{uniqueToken}@example.com")
            .WithPhoneNumber("11900010001")
            .BuildCreateRequest();

        var createCustomerResponse = await client.PostAsJsonAsync("/customers", customerRequest);
        HttpResponseAssertions.AssertStatus(createCustomerResponse, HttpStatusCode.Created);
        var createdCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(createCustomerResponse);

        var activatePortalResponse = await client.PostAsJsonAsync(
            $"/customers/{createdCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1990, 3, 14)));
        HttpResponseAssertions.AssertStatus(activatePortalResponse, HttpStatusCode.Created);
        var createdCustomerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activatePortalResponse);

        await AuthenticateCustomerPortalUserAsActiveAsync(
            client,
            createdCustomerUser.Email,
            createdCustomerUser.FullName,
            createdCustomerUser.BirthDate,
            "Customer.Policy.Customers#123");

        var forbiddenResponse = await client.GetAsync("/customers?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(forbiddenResponse, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostCustomer_ShouldReturn201_WhenRequestIsValid()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var request = CustomerSeed.CreateUniqueBuilder().BuildCreateRequest();

        var response = await client.PostAsJsonAsync("/customers", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(request.TaxDocument, payload.TaxDocument);
        Assert.Equal("John Doe", payload.FullName);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturn404_WhenCustomerDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/customers/{Guid.NewGuid()}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutCustomer_ShouldReturn200_WhenCustomerExists()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var customerId = await CustomerSeed.CreateIdAsync(
            client,
            new CustomerBuilder()
                .WithTaxDocument("153.509.460-56")
                .WithFullName("Jane Doe")
                .WithEmail("jane.doe@example.com")
                .WithPhoneNumber("21912345678"));
        var request = new CustomerBuilder()
            .WithFullName("John Updated")
            .WithEmail("john.updated@example.com")
            .WithPhoneNumber("11912345678")
            .BuildUpdateRequest();

        var response = await client.PutAsJsonAsync($"/customers/{customerId}", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(response);
        Assert.Equal(customerId, payload.Id);
        Assert.Equal("John Updated", payload.FullName);
        Assert.Equal("john.updated@example.com", payload.Email);
        Assert.Equal("11912345678", payload.PhoneNumber);
    }

    [Fact]
    public async Task DeleteCustomer_ShouldReturn204_WhenCustomerExists()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var customerId = await CustomerSeed.CreateIdAsync(
            client,
            new CustomerBuilder().WithTaxDocument("776.885.154-40"));

        var deleteResponse = await client.DeleteAsync($"/customers/{customerId}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/customers/{customerId}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutCustomer_ShouldReturn404_WhenCustomerDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var request = new CustomerBuilder()
            .WithFullName("Ghost User")
            .WithEmail("ghost@example.com")
            .WithPhoneNumber("11911111111")
            .BuildUpdateRequest();

        var response = await client.PutAsJsonAsync($"/customers/{Guid.NewGuid()}", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCustomer_ShouldReturn409_WhenCustomerHasRelatedVehicles()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var seeded = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder(),
            brandBuilder: new VehicleBrandBuilder().WithName($"Brand-{Guid.NewGuid():N}"),
            modelBuilder: new VehicleModelBuilder().WithName($"Model-{Guid.NewGuid():N}"),
            colorBuilder: new VehicleColorBuilder().WithName($"Color-{Guid.NewGuid():N}"),
            builder: new VehicleBuilder().WithPlate($"ABC{DateTime.UtcNow.Ticks % 10000:D4}"));

        var response = await client.DeleteAsync($"/customers/{seeded.CustomerId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Conflict);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(response);
        Assert.Equal("Business rule violation", payload.Title);
        Assert.Equal((int)HttpStatusCode.Conflict, payload.Status);
        Assert.Contains("cannot be deleted because it has related vehicles.", payload.Detail);

        var getResponse = await client.GetAsync($"/customers/{seeded.CustomerId}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.OK);
        var customer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(getResponse);
        Assert.Equal(seeded.CustomerId, customer.Id);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturnVehicles_WhenCustomerHasVehicles()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            builder: new VehicleBuilder()
                .WithPlate("CAR1234")
                .WithYear(2021),
            customerBuilder: CustomerSeed.CreateUniqueBuilder().WithFullName("Customer With Vehicle"),
            brandBuilder: new VehicleBrandBuilder().WithName("Tesla"),
            modelBuilder: new VehicleModelBuilder().WithName("Model X"),
            colorBuilder: new VehicleColorBuilder().WithName("Black"));

        var response = await client.GetAsync($"/customers/{seededVehicle.CustomerId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(response);
        Assert.Equal(seededVehicle.CustomerId, payload.Id);
        Assert.NotNull(payload.Vehicles);
        Assert.Contains(
            payload.Vehicles,
            vehicle => vehicle.Id == seededVehicle.VehicleId &&
                       vehicle.Year == 2021 &&
                       vehicle.VehicleBrandName == "Tesla" &&
                       vehicle.VehicleModelName == "Model X" &&
                       vehicle.VehicleColorName == "Black");
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturnEmptyVehicles_WhenCustomerHasNoVehicles()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var customerId = await CustomerSeed.CreateIdAsync(
            client,
            CustomerSeed.CreateUniqueBuilder().WithFullName("Customer Without Vehicle"));

        var response = await client.GetAsync($"/customers/{customerId}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(response);
        Assert.Equal(customerId, payload.Id);
        Assert.Empty(payload.Vehicles ?? Array.Empty<CustomerVehicleResponse>());
    }

    [Fact]
    public async Task ActiveStaff_ShouldActivateCustomerPortalUser_WhenCustomerExists()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var customerRequest = CustomerSeed.CreateUniqueBuilder()
            .WithFullName("Portal Integration Customer")
            .WithEmail($"portal.customer.{uniqueToken}@example.com")
            .WithPhoneNumber("11987654321")
            .BuildCreateRequest();

        var createCustomerResponse = await client.PostAsJsonAsync("/customers", customerRequest);
        HttpResponseAssertions.AssertStatus(createCustomerResponse, HttpStatusCode.Created);
        var createdCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(createCustomerResponse);

        var activateResponse = await client.PostAsJsonAsync(
            $"/customers/{createdCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1990, 1, 1)));

        HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(createdCustomer.Id, payload.CustomerId);
        Assert.Equal("portal.customer." + uniqueToken + "@example.com", payload.Email);
        Assert.Equal("Customer", payload.Role);
        Assert.True(payload.MustChangePassword);
    }

    [Fact]
    public async Task ActiveStaff_ShouldReceive409_WhenCustomerPortalUserIsActivatedTwice()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var customerRequest = CustomerSeed.CreateUniqueBuilder()
            .WithFullName("Portal Duplicate Customer")
            .WithEmail($"portal.duplicate.{uniqueToken}@example.com")
            .WithPhoneNumber("11912312312")
            .BuildCreateRequest();

        var createCustomerResponse = await client.PostAsJsonAsync("/customers", customerRequest);
        HttpResponseAssertions.AssertStatus(createCustomerResponse, HttpStatusCode.Created);
        var createdCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(createCustomerResponse);

        var firstActivationResponse = await client.PostAsJsonAsync(
            $"/customers/{createdCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1991, 2, 2)));
        HttpResponseAssertions.AssertStatus(firstActivationResponse, HttpStatusCode.Created);

        var duplicateActivationResponse = await client.PostAsJsonAsync(
            $"/customers/{createdCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1991, 2, 2)));

        HttpResponseAssertions.AssertStatus(duplicateActivationResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ActiveCustomer_ShouldReceive403_WhenActivatingCustomerPortalUser()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var firstCustomerRequest = CustomerSeed.CreateUniqueBuilder()
            .WithFullName("Portal Auth Customer One")
            .WithEmail($"portal.auth.one.{uniqueToken}@example.com")
            .WithPhoneNumber("11945645645")
            .BuildCreateRequest();
        var secondCustomerRequest = CustomerSeed.CreateUniqueBuilder()
            .WithFullName("Portal Auth Customer Two")
            .WithEmail($"portal.auth.two.{uniqueToken}@example.com")
            .WithPhoneNumber("11978978978")
            .BuildCreateRequest();

        var firstCustomerResponse = await client.PostAsJsonAsync("/customers", firstCustomerRequest);
        HttpResponseAssertions.AssertStatus(firstCustomerResponse, HttpStatusCode.Created);
        var firstCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(firstCustomerResponse);

        var secondCustomerResponse = await client.PostAsJsonAsync("/customers", secondCustomerRequest);
        HttpResponseAssertions.AssertStatus(secondCustomerResponse, HttpStatusCode.Created);
        var secondCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(secondCustomerResponse);

        var activatePortalResponse = await client.PostAsJsonAsync(
            $"/customers/{firstCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1992, 6, 10)));
        HttpResponseAssertions.AssertStatus(activatePortalResponse, HttpStatusCode.Created);
        var createdCustomerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activatePortalResponse);

        await AuthenticateCustomerPortalUserAsActiveAsync(
            client,
            createdCustomerUser.Email,
            createdCustomerUser.FullName,
            createdCustomerUser.BirthDate,
            "Customer.Active#123");

        var forbiddenActivationResponse = await client.PostAsJsonAsync(
            $"/customers/{secondCustomer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1993, 7, 11)));

        HttpResponseAssertions.AssertStatus(forbiddenActivationResponse, HttpStatusCode.Forbidden);
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
