using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Contracts.Common;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.InventoryItems;

public sealed class InventoryItemsE2eTests(E2eApiFixture fixture) : IClassFixture<E2eApiFixture>
{
    private const string NotFoundProblemTitle = "Resource not found";

    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task InventoryItems_ShouldSupportCreateListGetUpdateStockAndDeleteContracts()
    {
        // Given an authenticated attendant and a unique inventory item payload.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");
        _ = await client.AuthenticateAsActiveAttendantAsync(uniqueSeed);

        var createRequest = new CreateInventoryItemRequest(
            Name: $"E2E Item {uniqueSeed[..8]}",
            Description: $"Initial description {uniqueSeed[..8]}",
            Type: "Part",
            Cost: 35.50m,
            Price: 62.00m,
            StockQuantity: 8);

        // When the attendant creates the inventory item.
        using var createResponse = await client.PostAsJsonAsync("/inventory-items", createRequest);

        // Then the created item preserves commercial fields and initial stock.
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);

        var createdItem = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, createdItem.Id);
        Assert.Equal(createRequest.Name, createdItem.Name);
        Assert.Equal(createRequest.Description, createdItem.Description);
        Assert.Equal(createRequest.Type, createdItem.Type);
        Assert.Equal(createRequest.Cost, createdItem.Cost);
        Assert.Equal(createRequest.Price, createdItem.Price);
        Assert.Equal(createRequest.StockQuantity, createdItem.StockQuantity);
        Assert.NotEqual(default, createdItem.CreatedAt);

        // When the attendant lists inventory items.
        using var listResponse = await client.GetAsync("/inventory-items?page=1&pageSize=20");

        // Then the created item appears in the paginated response.
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);

        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<InventoryItemResponse>>(listResponse);
        Assert.Equal(1, listPayload.Page);
        Assert.Equal(20, listPayload.PageSize);
        Assert.True(listPayload.TotalCount >= 1);

        var listedItem = listPayload.Items.FirstOrDefault(item => item.Id == createdItem.Id);
        Assert.NotNull(listedItem);
        Assert.Equal(createdItem.Name, listedItem!.Name);
        Assert.Equal(createdItem.Description, listedItem.Description);
        Assert.Equal(createdItem.Type, listedItem.Type);
        Assert.Equal(createdItem.Cost, listedItem.Cost);
        Assert.Equal(createdItem.Price, listedItem.Price);
        Assert.Equal(createdItem.StockQuantity, listedItem.StockQuantity);
        Assert.NotEqual(default, listedItem.CreatedAt);

        // When the attendant fetches the item by id.
        using var getByIdResponse = await client.GetAsync($"/inventory-items/{createdItem.Id}");

        // Then the details contract matches the created item.
        HttpResponseAssertions.AssertStatus(getByIdResponse, HttpStatusCode.OK);

        var getByIdPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(getByIdResponse);
        Assert.Equal(createdItem.Id, getByIdPayload.Id);
        Assert.Equal(createdItem.Name, getByIdPayload.Name);
        Assert.Equal(createdItem.Description, getByIdPayload.Description);
        Assert.Equal(createdItem.Type, getByIdPayload.Type);
        Assert.Equal(createdItem.Cost, getByIdPayload.Cost);
        Assert.Equal(createdItem.Price, getByIdPayload.Price);
        Assert.Equal(createdItem.StockQuantity, getByIdPayload.StockQuantity);
        Assert.NotEqual(default, getByIdPayload.CreatedAt);

        var updateRequest = new UpdateInventoryItemRequest(
            Name: $"E2E Item Updated {uniqueSeed[..8]}",
            Description: $"Updated description {uniqueSeed[..8]}",
            Type: "Supply",
            Cost: 42.10m,
            Price: 79.99m);

        // When the attendant updates item metadata and prices.
        using var updateResponse = await client.PutAsJsonAsync($"/inventory-items/{createdItem.Id}", updateRequest);

        // Then the update response changes those fields while preserving stock.
        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);

        var updatedPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(updateResponse);
        Assert.Equal(createdItem.Id, updatedPayload.Id);
        Assert.Equal(updateRequest.Name, updatedPayload.Name);
        Assert.Equal(updateRequest.Description, updatedPayload.Description);
        Assert.Equal(updateRequest.Type, updatedPayload.Type);
        Assert.Equal(updateRequest.Cost, updatedPayload.Cost);
        Assert.Equal(updateRequest.Price, updatedPayload.Price);
        Assert.Equal(createRequest.StockQuantity, updatedPayload.StockQuantity);
        Assert.NotEqual(default, updatedPayload.CreatedAt);

        // When the attendant updates stock independently.
        var stockQuantity = 21;
        using var updateStockResponse = await client.PatchAsJsonAsync(
            $"/inventory-items/{createdItem.Id}/stock",
            new UpdateInventoryItemStockRequest(stockQuantity));

        // Then only the stock quantity changes in the response contract.
        HttpResponseAssertions.AssertStatus(updateStockResponse, HttpStatusCode.OK);

        var stockPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(updateStockResponse);
        Assert.Equal(createdItem.Id, stockPayload.Id);
        Assert.Equal(updateRequest.Name, stockPayload.Name);
        Assert.Equal(updateRequest.Description, stockPayload.Description);
        Assert.Equal(updateRequest.Type, stockPayload.Type);
        Assert.Equal(updateRequest.Cost, stockPayload.Cost);
        Assert.Equal(updateRequest.Price, stockPayload.Price);
        Assert.Equal(stockQuantity, stockPayload.StockQuantity);
        Assert.NotEqual(default, stockPayload.CreatedAt);

        // When the attendant deletes the inventory item.
        using var deleteResponse = await client.DeleteAsync($"/inventory-items/{createdItem.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        // Then subsequent reads return the expected not-found ProblemDetails contract.
        using var getDeletedResponse = await client.GetAsync($"/inventory-items/{createdItem.Id}");
        HttpResponseAssertions.AssertStatus(getDeletedResponse, HttpStatusCode.NotFound);

        var notFoundProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(getDeletedResponse);
        Assert.Equal((int)HttpStatusCode.NotFound, notFoundProblem.Status);
        Assert.Equal(NotFoundProblemTitle, notFoundProblem.Title);
        Assert.False(string.IsNullOrWhiteSpace(notFoundProblem.Type));
    }

    private sealed record CreateInventoryItemRequest(
        string Name,
        string Description,
        string Type,
        decimal Cost,
        decimal Price,
        int StockQuantity);

    private sealed record UpdateInventoryItemRequest(
        string Name,
        string Description,
        string Type,
        decimal Cost,
        decimal Price);

    private sealed record UpdateInventoryItemStockRequest(int StockQuantity);

    private sealed record InventoryItemResponse(
        Guid Id,
        string Name,
        string Description,
        string Type,
        decimal Cost,
        decimal Price,
        int StockQuantity,
        DateTime CreatedAt);

}
