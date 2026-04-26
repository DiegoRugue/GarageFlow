using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Customers.Contracts;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Customers;
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
}

