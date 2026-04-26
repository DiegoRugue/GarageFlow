using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Customers.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Customers;

namespace GarageFlow.Tests.Integration.Api.Customers;

public class CustomersApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task PostCustomer_ShouldReturn201_WhenRequestIsValid()
    {
        using var client = _fixture.CreateClient();
        var request = new CustomerBuilder().BuildCreateRequest();

        var response = await client.PostAsJsonAsync("/customers", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal("52998224725", payload.TaxDocument);
        Assert.Equal("John Doe", payload.FullName);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturn404_WhenCustomerDoesNotExist()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/customers/{Guid.NewGuid()}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutCustomer_ShouldReturn200_WhenCustomerExists()
    {
        using var client = _fixture.CreateClient();
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
        using var client = _fixture.CreateClient();
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
        using var client = _fixture.CreateClient();
        var request = new CustomerBuilder()
            .WithFullName("Ghost User")
            .WithEmail("ghost@example.com")
            .WithPhoneNumber("11911111111")
            .BuildUpdateRequest();

        var response = await client.PutAsJsonAsync($"/customers/{Guid.NewGuid()}", request);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }
}