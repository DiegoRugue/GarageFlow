using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Contracts.Common;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.Services;

[Collection(E2eApiCollection.Name)]
public sealed class ServicesE2eTests(E2eApiFixture fixture)
{
    private const string NotFoundProblemTitle = "Resource not found";

    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task Services_ShouldSupportCreateListGetUpdateAndDeleteContracts()
    {
        // Given an authenticated admin and a unique service payload.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");

        var createRequest = new CreateServiceRequest(
            Description: $"E2E Service {uniqueSeed[..8]}",
            Price: 149.90m);

        // When the admin creates the service.
        using var createResponse = await client.PostAsJsonAsync("/services", createRequest);

        // Then the service is created with the submitted description and price.
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);

        var createdService = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateServiceResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, createdService.Id);
        Assert.Equal(createRequest.Description, createdService.Description);
        Assert.Equal(createRequest.Price, createdService.Price);
        Assert.NotEqual(default, createdService.CreatedAt);

        // When the admin lists services.
        using var listResponse = await client.GetAsync("/services?page=1&pageSize=20");

        // Then the created service appears in the paginated response.
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);

        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<ServiceResponse>>(listResponse);
        Assert.Equal(1, listPayload.Page);
        Assert.Equal(20, listPayload.PageSize);
        Assert.True(listPayload.TotalCount >= 1);

        var listedService = listPayload.Items.FirstOrDefault(item => item.Id == createdService.Id);
        Assert.NotNull(listedService);
        Assert.Equal(createdService.Description, listedService!.Description);
        Assert.Equal(createdService.Price, listedService.Price);
        Assert.NotEqual(default, listedService.CreatedAt);

        // When the admin fetches the service by id.
        using var getByIdResponse = await client.GetAsync($"/services/{createdService.Id}");

        // Then the service details match the created contract.
        HttpResponseAssertions.AssertStatus(getByIdResponse, HttpStatusCode.OK);

        var getByIdPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(getByIdResponse);
        Assert.Equal(createdService.Id, getByIdPayload.Id);
        Assert.Equal(createdService.Description, getByIdPayload.Description);
        Assert.Equal(createdService.Price, getByIdPayload.Price);
        Assert.NotEqual(default, getByIdPayload.CreatedAt);

        var updateRequest = new UpdateServiceRequest(
            Description: $"E2E Updated Service {uniqueSeed[..8]}",
            Price: 199.75m);

        // When the admin updates the service.
        using var updateResponse = await client.PutAsJsonAsync($"/services/{createdService.Id}", updateRequest);

        // Then the updated response contains the new description and price.
        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);

        var updatedPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(updateResponse);
        Assert.Equal(createdService.Id, updatedPayload.Id);
        Assert.Equal(updateRequest.Description, updatedPayload.Description);
        Assert.Equal(updateRequest.Price, updatedPayload.Price);
        Assert.NotEqual(default, updatedPayload.CreatedAt);

        // When the admin deletes the service.
        using var deleteResponse = await client.DeleteAsync($"/services/{createdService.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        // Then subsequent reads return the expected not-found ProblemDetails contract.
        using var getDeletedResponse = await client.GetAsync($"/services/{createdService.Id}");
        HttpResponseAssertions.AssertStatus(getDeletedResponse, HttpStatusCode.NotFound);

        var notFoundProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(getDeletedResponse);
        Assert.Equal((int)HttpStatusCode.NotFound, notFoundProblem.Status);
        Assert.Equal(NotFoundProblemTitle, notFoundProblem.Title);
        Assert.False(string.IsNullOrWhiteSpace(notFoundProblem.Type));
    }

    private sealed record CreateServiceRequest(string Description, decimal Price);

    private sealed record CreateServiceResponse(
        Guid Id,
        string Description,
        decimal Price,
        DateTime CreatedAt);

    private sealed record UpdateServiceRequest(string Description, decimal Price);

    private sealed record ServiceResponse(
        Guid Id,
        string Description,
        decimal Price,
        DateTime CreatedAt);
}
