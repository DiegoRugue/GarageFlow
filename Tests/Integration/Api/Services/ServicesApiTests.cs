using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Services.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Shared.Services;

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
}
