using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Tests.E2E.Support.Contracts.Auth;
using GarageFlow.Tests.E2E.Support.Contracts.Common;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;
using GarageFlow.Tests.Shared.WorkOrders;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GarageFlow.Tests.E2E.WorkOrders;

[Collection(E2eApiCollection.Name)]
public sealed class WorkOrdersE2eTests(E2eApiFixture fixture)
{
    private const string NotFoundProblemTitle = "Resource not found";
    private const string ConflictProblemTitle = "Business rule violation";

    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task WorkOrders_ShouldSupportEstimateApprovalAndCompletionJourney()
    {
        // Given an authenticated staff user and a complete customer/vehicle/service/inventory setup.
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");
        var setup = await CreateWorkOrderSetupAsync(staffClient, uniqueSeed);

        // When staff opens a work order for the customer's vehicle.
        using var createWorkOrderResponse = await staffClient.PostAsJsonAsync(
            "/work-orders",
            new CreateWorkOrderRequest(setup.CustomerId, setup.VehicleId));

        // Then the work order starts in the Received state.
        HttpResponseAssertions.AssertStatus(createWorkOrderResponse, HttpStatusCode.Created);
        Assert.NotNull(createWorkOrderResponse.Headers.Location);

        var createdWorkOrder = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderResponse>(createWorkOrderResponse);
        Assert.NotEqual(Guid.Empty, createdWorkOrder.Id);
        Assert.Equal(setup.CustomerId, createdWorkOrder.CustomerId);
        Assert.Equal(setup.VehicleId, createdWorkOrder.VehicleId);
        Assert.Equal("Received", createdWorkOrder.Status);
        Assert.NotEqual(default, createdWorkOrder.CreatedAt);

        // When staff creates the first estimate for the work order.
        using var createEstimateResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates",
            new { });

        // Then the estimate starts as a zero-total draft.
        HttpResponseAssertions.AssertStatus(createEstimateResponse, HttpStatusCode.Created);

        var createdEstimate = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateEstimateResponse>(createEstimateResponse);
        Assert.NotEqual(Guid.Empty, createdEstimate.Id);
        Assert.Equal(createdWorkOrder.Id, createdEstimate.WorkOrderId);
        Assert.Equal("Draft", createdEstimate.Status);
        Assert.Equal(0m, createdEstimate.TotalAmount);
        Assert.NotEqual(default, createdEstimate.CreatedAt);

        // When staff adds a service line to the estimate.
        using var addServiceResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/services",
            new AddEstimateServiceRequest(setup.ServiceId));

        // Then the service line captures the current service description and price.
        HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

        var addedService = await HttpResponseAssertions.ReadRequiredJsonAsync<AddEstimateServiceResponse>(addServiceResponse);
        Assert.Equal(createdEstimate.Id, addedService.EstimateId);
        Assert.Equal(setup.ServiceId, addedService.ServiceId);
        Assert.Equal(setup.ServiceDescription, addedService.Description);
        Assert.Equal(setup.ServicePrice, addedService.UnitPrice);
        Assert.Equal(setup.ServicePrice, addedService.TotalPrice);

        // And when staff adds an inventory line with quantity.
        const int inventoryQuantity = 2;
        using var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(setup.InventoryItemId, inventoryQuantity));

        // Then the inventory line calculates total price and preserves cost/price details.
        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        var addedInventory = await HttpResponseAssertions.ReadRequiredJsonAsync<AddEstimateInventoryItemResponse>(addInventoryResponse);
        Assert.Equal(createdEstimate.Id, addedInventory.EstimateId);
        Assert.Equal(setup.InventoryItemId, addedInventory.InventoryItemId);
        Assert.Equal(setup.InventoryItemDescription, addedInventory.Description);
        Assert.Equal(inventoryQuantity, addedInventory.Quantity);
        Assert.Equal(setup.InventoryItemCost, addedInventory.UnitCost);
        Assert.Equal(setup.InventoryItemPrice, addedInventory.UnitPrice);
        Assert.Equal(setup.InventoryItemPrice * inventoryQuantity, addedInventory.TotalPrice);

        // When staff submits the estimate for customer approval.
        using var submitEstimateResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/submit",
            content: null);

        // Then the submission is accepted without a response body.
        HttpResponseAssertions.AssertStatus(submitEstimateResponse, HttpStatusCode.NoContent);

        // Given the customer portal user is activated and authenticated.
        using var customerClient = await CreateAuthenticatedCustomerPortalClientAsync(
            staffClient,
            setup.CustomerId,
            uniqueSeed);

        // When the customer approves the submitted estimate.
        using var approveEstimateResponse = await customerClient.PostAsync(
            $"/me/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/approve",
            content: null);

        // Then the approval is accepted and the work order enters execution.
        HttpResponseAssertions.AssertStatus(approveEstimateResponse, HttpStatusCode.NoContent);

        using var detailsAfterApprovalResponse = await staffClient.GetAsync($"/work-orders/{createdWorkOrder.Id}");
        HttpResponseAssertions.AssertStatus(detailsAfterApprovalResponse, HttpStatusCode.OK);
        var detailsAfterApproval = await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(detailsAfterApprovalResponse);
        Assert.Equal("InProgress", detailsAfterApproval.Status);

        using (var approvalScope = _fixture.CreateScope())
        {
            var approvalContext = approvalScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
            var approvalNotifications = (await approvalContext.IntegrationOutboxMessages
                    .AsNoTracking()
                    .Where(message => message.AggregateId == createdWorkOrder.Id)
                    .ToListAsync())
                .Select(message => IntegrationEventJson.Deserialize<WorkOrderStatusChangedIntegrationEvent>(message.Payload))
                .Where(notification => notification is
                {
                    PreviousStatus: "WaitingApproval",
                    CurrentStatus: "InProgress"
                })
                .ToList();
            var approvalNotification = Assert.Single(approvalNotifications);
            Assert.NotNull(approvalNotification);
            Assert.Equal(createdWorkOrder.Id, approvalNotification.WorkOrderId);
        }

        var estimateAfterApproval = Assert.Single(detailsAfterApproval.Estimates);
        Assert.Equal(createdEstimate.Id, estimateAfterApproval.Id);
        Assert.Equal("Approved", estimateAfterApproval.Status);
        var approvedServiceLine = Assert.Single(estimateAfterApproval.ServiceLines);
        Assert.Equal("Pending", approvedServiceLine.Status);
        Assert.Null(approvedServiceLine.StartedAt);
        Assert.Null(approvedServiceLine.CompletedAt);

        // When staff starts and completes the approved service line.
        using var startServiceResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/services/{approvedServiceLine.Id}/start",
            content: null);

        HttpResponseAssertions.AssertStatus(startServiceResponse, HttpStatusCode.NoContent);

        using var completeServiceResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/services/{approvedServiceLine.Id}/complete",
            content: null);

        HttpResponseAssertions.AssertStatus(completeServiceResponse, HttpStatusCode.NoContent);

        // And staff delivers the work order.
        using var deliverResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/deliver",
            content: null);

        HttpResponseAssertions.AssertStatus(deliverResponse, HttpStatusCode.NoContent);

        // Then final details show a delivered work order with approved estimate and completed service.
        using var detailsResponse = await staffClient.GetAsync($"/work-orders/{createdWorkOrder.Id}");
        HttpResponseAssertions.AssertStatus(detailsResponse, HttpStatusCode.OK);

        var details = await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(detailsResponse);
        Assert.Equal(createdWorkOrder.Id, details.Id);
        Assert.Equal(setup.CustomerId, details.CustomerId);
        Assert.Equal(setup.VehicleId, details.VehicleId);
        Assert.Equal("Delivered", details.Status);
        Assert.NotEqual(default, details.CreatedAt);
        Assert.NotEqual(default, details.UpdatedAt);

        var estimate = Assert.Single(details.Estimates);
        Assert.Equal(createdEstimate.Id, estimate.Id);
        Assert.Equal(createdWorkOrder.Id, estimate.WorkOrderId);
        Assert.Equal("Approved", estimate.Status);
        Assert.Equal(setup.ServicePrice + (setup.InventoryItemPrice * inventoryQuantity), estimate.TotalAmount);
        Assert.NotEqual(default, estimate.CreatedAt);
        Assert.NotEqual(default, estimate.UpdatedAt);

        var inventoryLine = Assert.Single(estimate.InventoryLines);
        Assert.NotEqual(Guid.Empty, inventoryLine.Id);
        Assert.Equal(createdEstimate.Id, inventoryLine.EstimateId);
        Assert.Equal(setup.InventoryItemId, inventoryLine.InventoryItemId);
        Assert.Equal(setup.InventoryItemDescription, inventoryLine.Description);
        Assert.Equal(inventoryQuantity, inventoryLine.Quantity);
        Assert.Equal(setup.InventoryItemCost, inventoryLine.UnitCost);
        Assert.Equal(setup.InventoryItemPrice, inventoryLine.UnitPrice);
        Assert.Equal(setup.InventoryItemPrice * inventoryQuantity, inventoryLine.TotalPrice);

        var serviceLine = Assert.Single(estimate.ServiceLines);
        Assert.Equal(approvedServiceLine.Id, serviceLine.Id);
        Assert.Equal(createdEstimate.Id, serviceLine.EstimateId);
        Assert.Equal(setup.ServiceId, serviceLine.ServiceId);
        Assert.Equal(setup.ServiceDescription, serviceLine.Description);
        Assert.Equal(setup.ServicePrice, serviceLine.UnitPrice);
        Assert.Equal(setup.ServicePrice, serviceLine.TotalPrice);
        Assert.Equal("Completed", serviceLine.Status);
        Assert.NotNull(serviceLine.StartedAt);
        Assert.NotNull(serviceLine.CompletedAt);
        Assert.True(serviceLine.StartedAt <= serviceLine.CompletedAt);

        // And the focused staff status endpoint exposes only the status projection.
        using var statusResponse = await staffClient.GetAsync($"/work-orders/{createdWorkOrder.Id}/status");
        HttpResponseAssertions.AssertStatus(statusResponse, HttpStatusCode.OK);
        using var statusDocument = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            ["id", "status", "updatedAt"],
            statusDocument.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(createdWorkOrder.Id, statusDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Delivered", statusDocument.RootElement.GetProperty("status").GetString());

        // Delivered orders leave the active staff queue but remain in the customer's complete history.
        using var staffQueueResponse = await staffClient.GetAsync("/work-orders?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(staffQueueResponse, HttpStatusCode.OK);
        var staffQueue = await HttpResponseAssertions.ReadRequiredJsonAsync<ListWorkOrdersResponse>(staffQueueResponse);
        Assert.DoesNotContain(staffQueue.Items, item => item.Id == createdWorkOrder.Id);

        using var customerHistoryResponse = await customerClient.GetAsync("/me/work-orders?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(customerHistoryResponse, HttpStatusCode.OK);
        var customerHistory = await HttpResponseAssertions.ReadRequiredJsonAsync<ListWorkOrdersResponse>(customerHistoryResponse);
        Assert.Contains(customerHistory.Items, item => item.Id == createdWorkOrder.Id && item.Status == "Delivered");
    }

    [Fact]
    public async Task WorkOrders_ShouldTranslateActiveQueueOrderingFilteringAndPaging_InPostgreSql()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var seed = Guid.NewGuid().ToString("N");
        var targetSetup = await CreateWorkOrderSetupAsync(staffClient, seed);
        var otherSetup = await CreateWorkOrderSetupAsync(staffClient, Guid.NewGuid().ToString("N"));

        var inProgressOldest = await CreateWorkOrderAsync(staffClient, targetSetup);
        var inProgressNewest = await CreateWorkOrderAsync(staffClient, targetSetup);
        var waitingApproval = await CreateWorkOrderAsync(staffClient, targetSetup);
        var diagnosing = await CreateWorkOrderAsync(staffClient, targetSetup);
        var receivedFirst = await CreateWorkOrderAsync(staffClient, targetSetup);
        var receivedSecond = await CreateWorkOrderAsync(staffClient, targetSetup);
        var completed = await CreateWorkOrderAsync(staffClient, targetSetup);
        var delivered = await CreateWorkOrderAsync(staffClient, targetSetup);
        var cancelled = await CreateWorkOrderAsync(staffClient, targetSetup);
        var otherCustomerReceived = await CreateWorkOrderAsync(staffClient, otherSetup);

        var idPrefix = Guid.NewGuid().ToString("N")[..30];
        var receivedLowerId = Guid.ParseExact($"{idPrefix}01", "N");
        var receivedHigherId = Guid.ParseExact($"{idPrefix}02", "N");
        var baseline = new DateTime(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc);

        await SeedWorkOrderQueueAsync(
        [
            new(inProgressOldest.Id, inProgressOldest.Id, "InProgress", baseline),
            new(inProgressNewest.Id, inProgressNewest.Id, "InProgress", baseline.AddMinutes(1)),
            new(waitingApproval.Id, waitingApproval.Id, "WaitingApproval", baseline),
            new(diagnosing.Id, diagnosing.Id, "Diagnosing", baseline),
            new(receivedFirst.Id, receivedLowerId, "Received", baseline),
            new(receivedSecond.Id, receivedHigherId, "Received", baseline),
            new(completed.Id, completed.Id, "Completed", baseline),
            new(delivered.Id, delivered.Id, "Delivered", baseline),
            new(cancelled.Id, cancelled.Id, "Cancelled", baseline),
            new(otherCustomerReceived.Id, otherCustomerReceived.Id, "Received", baseline)
        ]);

        Guid[] expectedIds =
        [
            inProgressOldest.Id,
            inProgressNewest.Id,
            waitingApproval.Id,
            diagnosing.Id,
            receivedLowerId,
            receivedHigherId
        ];
        Guid[] terminalIds = [completed.Id, delivered.Id, cancelled.Id];

        using var completeResponse = await staffClient.GetAsync(
            $"/work-orders?page=1&pageSize=100&customerId={targetSetup.CustomerId}");
        HttpResponseAssertions.AssertStatus(completeResponse, HttpStatusCode.OK);
        var completeQueue = await HttpResponseAssertions.ReadRequiredJsonAsync<ListWorkOrdersResponse>(completeResponse);

        Assert.Equal(6, completeQueue.TotalCount);
        Assert.Equal(expectedIds, completeQueue.Items.Select(item => item.Id));
        Assert.Equal(
            ["InProgress", "InProgress", "WaitingApproval", "Diagnosing", "Received", "Received"],
            completeQueue.Items.Select(item => item.Status));
        Assert.DoesNotContain(completeQueue.Items, item => terminalIds.Contains(item.Id));
        Assert.DoesNotContain(completeQueue.Items, item => item.Id == otherCustomerReceived.Id);

        var pagedIds = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            using var pageResponse = await staffClient.GetAsync(
                $"/work-orders?page={page}&pageSize=2&customerId={targetSetup.CustomerId}");
            HttpResponseAssertions.AssertStatus(pageResponse, HttpStatusCode.OK);
            var pagePayload = await HttpResponseAssertions.ReadRequiredJsonAsync<ListWorkOrdersResponse>(pageResponse);

            Assert.Equal(6, pagePayload.TotalCount);
            Assert.Equal(page, pagePayload.Page);
            Assert.Equal(2, pagePayload.PageSize);
            Assert.Equal(2, pagePayload.Items.Count);
            pagedIds.AddRange(pagePayload.Items.Select(item => item.Id));
        }

        Assert.Equal(expectedIds, pagedIds);
        Assert.Equal(expectedIds.Length, pagedIds.Distinct().Count());
    }

    [Fact]
    public async Task WorkOrderIntake_ShouldSerializeConcurrentIdenticalRequests_AndRejectChangedReplay()
    {
        using var firstClient = await _fixture.CreateAuthenticatedClientAsync();
        using var secondClient = await _fixture.CreateAuthenticatedClientAsync();
        var request = CreateIntakeRequest();

        var firstTask = firstClient.PostAsJsonAsync("/work-orders/intake", request);
        var secondTask = secondClient.PostAsJsonAsync("/work-orders/intake", request);
        var responses = await Task.WhenAll(firstTask, secondTask);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Created],
            responses.Select(response => response.StatusCode).Order().ToArray());
        var first = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderIntakeResponse>(firstResponse);
        var second = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderIntakeResponse>(secondResponse);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.NotEqual(Guid.Empty, first.WorkOrderId);
        Assert.NotEqual(Guid.Empty, first.CustomerId);
        Assert.NotEqual(Guid.Empty, first.VehicleId);

        using var detailsResponse = await firstClient.GetAsync($"/work-orders/{first.WorkOrderId}");
        HttpResponseAssertions.AssertStatus(detailsResponse, HttpStatusCode.OK);
        var details = await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(detailsResponse);
        Assert.Equal(first.CustomerId, details.CustomerId);
        Assert.Equal(first.VehicleId, details.VehicleId);
        Assert.Equal(first.EstimateId, Assert.Single(details.Estimates).Id);

        using var listResponse = await firstClient.GetAsync("/work-orders?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var list = await HttpResponseAssertions.ReadRequiredJsonAsync<ListWorkOrdersResponse>(listResponse);
        var listed = Assert.Single(
            list.Items,
            item => item.CustomerId == first.CustomerId && item.VehicleId == first.VehicleId);
        Assert.Equal(first.WorkOrderId, listed.Id);
        Assert.Equal(first.CustomerId, listed.CustomerId);
        Assert.Equal(first.VehicleId, listed.VehicleId);
        var listedEstimate = Assert.Single(listed.Estimates);
        Assert.Equal(first.EstimateId, listedEstimate.Id);
        Assert.Equal(Assert.Single(first.ServiceIds), Assert.Single(listedEstimate.ServiceLines).ServiceId);
        Assert.Equal(
            Assert.Single(first.InventoryItemIds),
            Assert.Single(listedEstimate.InventoryLines).InventoryItemId);

        using var customersResponse = await firstClient.GetAsync("/customers?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(customersResponse, HttpStatusCode.OK);
        var customers = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<CreateCustomerResponse>>(
            customersResponse);
        var persistedCustomer = Assert.Single(
            customers.Items,
            customer => customer.TaxDocument == request.Customer!.TaxDocument);
        Assert.Equal(first.CustomerId, persistedCustomer.Id);

        using var vehiclesResponse = await firstClient.GetAsync("/vehicles?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(vehiclesResponse, HttpStatusCode.OK);
        var vehicles = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<CreateVehicleResponse>>(
            vehiclesResponse);
        var persistedVehicle = Assert.Single(
            vehicles.Items,
            vehicle => vehicle.Plate == request.Vehicle!.Plate);
        Assert.Equal(first.VehicleId, persistedVehicle.Id);
        Assert.Equal(first.CustomerId, persistedVehicle.CustomerId);

        using var servicesResponse = await firstClient.GetAsync("/services?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(servicesResponse, HttpStatusCode.OK);
        var services = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<ServiceResponse>>(
            servicesResponse);
        var requestedService = Assert.Single(request.Services!);
        var persistedService = Assert.Single(
            services.Items,
            service => service.Description == requestedService.Description);
        Assert.Equal(Assert.Single(first.ServiceIds), persistedService.Id);

        using var inventoryClient = await _fixture.CreateAuthenticatedClientAsync();
        _ = await inventoryClient.AuthenticateAsActiveAttendantAsync(Guid.NewGuid().ToString("N"));
        using var inventoryItemsResponse = await inventoryClient.GetAsync("/inventory-items?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(inventoryItemsResponse, HttpStatusCode.OK);
        var inventoryItems = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<InventoryItemResponse>>(
            inventoryItemsResponse);
        var requestedInventoryItem = Assert.Single(request.InventoryItems!);
        var persistedInventoryItem = Assert.Single(
            inventoryItems.Items,
            item => item.Name == requestedInventoryItem.Name);
        Assert.Equal(Assert.Single(first.InventoryItemIds), persistedInventoryItem.Id);

        var changed = request with
        {
            Services = [new IntakeServiceRequest("Oil and filter replacement", 181m)]
        };
        using var conflictResponse = await firstClient.PostAsJsonAsync("/work-orders/intake", changed);

        HttpResponseAssertions.AssertStatus(conflictResponse, HttpStatusCode.Conflict);
        var problem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(conflictResponse);
        Assert.Equal((int)HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ConflictProblemTitle, problem.Title);
    }

    [Fact]
    public async Task WorkOrders_ShouldReturnForbidden_ForCustomerListingStaffWorkOrders()
    {
        // Given a customer portal user authenticated for their own customer account.
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");
        var setup = await CreateWorkOrderSetupAsync(staffClient, uniqueSeed);

        using var customerClient = await CreateAuthenticatedCustomerPortalClientAsync(
            staffClient,
            setup.CustomerId,
            uniqueSeed);

        // When that customer attempts to access the staff work-order listing.
        using var response = await customerClient.GetAsync("/work-orders?page=1&pageSize=20");

        // Then the route is forbidden by authorization policy.
        // Authz middleware currently returns 403 with an empty body for this route.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WorkOrders_ShouldReturnNotFound_ForMissingWorkOrder()
    {
        // Given an authenticated staff user and a work-order id that does not exist.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var missingId = Guid.NewGuid();

        // When staff requests that work-order details endpoint.
        using var response = await client.GetAsync($"/work-orders/{missingId}");

        // Then the API returns the not-found ProblemDetails contract.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);

        var problem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.Equal((int)HttpStatusCode.NotFound, problem.Status);
        Assert.Equal(NotFoundProblemTitle, problem.Title);
        Assert.Equal($"Work order with ID '{missingId}' was not found.", problem.Detail);
    }

    [Fact]
    public async Task WorkOrders_ShouldReturnNotFound_ForRemovedExecutionRoute()
    {
        // Given staff created a work order and submitted an estimate that still awaits customer approval.
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");
        var setup = await CreateWorkOrderSetupAsync(staffClient, uniqueSeed);

        using var createWorkOrderResponse = await staffClient.PostAsJsonAsync(
            "/work-orders",
            new CreateWorkOrderRequest(setup.CustomerId, setup.VehicleId));

        HttpResponseAssertions.AssertStatus(createWorkOrderResponse, HttpStatusCode.Created);
        var createdWorkOrder = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderResponse>(createWorkOrderResponse);

        using var createEstimateResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates",
            new { });

        HttpResponseAssertions.AssertStatus(createEstimateResponse, HttpStatusCode.Created);
        var createdEstimate = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateEstimateResponse>(createEstimateResponse);

        using var addServiceResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/services",
            new AddEstimateServiceRequest(setup.ServiceId));

        HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

        using var submitEstimateResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/submit",
            content: null);

        HttpResponseAssertions.AssertStatus(submitEstimateResponse, HttpStatusCode.NoContent);

        // When staff calls the removed execution route.
        using var removedRouteResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/start-work",
            content: null);

        // Then the API no longer advertises or handles it.
        HttpResponseAssertions.AssertStatus(removedRouteResponse, HttpStatusCode.NotFound);
    }

    private async Task<WorkOrderSetupIds> CreateWorkOrderSetupAsync(HttpClient staffClient, string seed)
    {
        var customer = await CreateCustomerAsync(staffClient, seed);
        var vehicle = await CreateVehicleAsync(staffClient, customer.Id, seed);
        var service = await CreateServiceAsync(staffClient, seed);
        var inventoryItem = await CreateInventoryItemAsync(seed);

        return new WorkOrderSetupIds(
            customer.Id,
            vehicle.Id,
            service.Id,
            inventoryItem.Id,
            service.Description,
            service.Price,
            inventoryItem.Description,
            inventoryItem.Cost,
            inventoryItem.Price);
    }

    private static CreateWorkOrderIntakeRequest CreateIntakeRequest() => new(
        Guid.NewGuid(),
        new IntakeCustomerRequest(
            "52998224725",
            "Maria Oliveira",
            "maria@example.com",
            "+5511999999999"),
        new IntakeVehicleRequest("ABC1D23", 2022, "Toyota", "Corolla", "Black"),
        [new IntakeServiceRequest("Oil and filter replacement", 180m)],
        [
            new IntakeInventoryItemRequest(
                "5W30 engine oil",
                "Synthetic engine oil",
                "Part",
                35m,
                55m,
                StockQuantity: 10,
                Quantity: 4)
        ]);

    private static async Task<CreateCustomerResponse> CreateCustomerAsync(HttpClient client, string seed)
    {
        var request = new CreateCustomerRequest(
            TaxDocument: GenerateValidCpf(seed),
            FullName: $"E2E WorkOrder Customer {seed[..8]}",
            Email: $"e2e-workorder-customer-{seed}@garageflow.local",
            PhoneNumber: GeneratePhoneNumber(seed));

        using var response = await client.PostAsJsonAsync("/customers", request);
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateCustomerResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(request.TaxDocument, payload.TaxDocument);
        Assert.Equal("Cpf", payload.TaxDocumentType);
        Assert.Equal(request.FullName, payload.FullName);
        Assert.Equal(request.Email, payload.Email);
        Assert.Equal(request.PhoneNumber, payload.PhoneNumber);
        Assert.NotEqual(default, payload.CreatedAt);

        return payload;
    }

    private static async Task<CreateWorkOrderResponse> CreateWorkOrderAsync(
        HttpClient client,
        WorkOrderSetupIds setup)
    {
        using var response = await client.PostAsJsonAsync(
            "/work-orders",
            new CreateWorkOrderRequest(setup.CustomerId, setup.VehicleId));
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderResponse>(response);
    }

    private async Task SeedWorkOrderQueueAsync(IReadOnlyCollection<WorkOrderQueueSeed> seeds)
    {
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        foreach (var seed in seeds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE "WorkOrders"
                SET "Id" = @seededId,
                    "Status" = @status,
                    "CreatedAt" = @createdAt,
                    "UpdatedAt" = @createdAt
                WHERE "Id" = @currentId;
                """;
            command.Parameters.AddWithValue("seededId", seed.SeededId);
            command.Parameters.AddWithValue("status", seed.Status);
            command.Parameters.AddWithValue("createdAt", seed.CreatedAt);
            command.Parameters.AddWithValue("currentId", seed.CurrentId);

            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        await transaction.CommitAsync();
    }

    private static async Task<CreatedVehicleDependencies> CreateVehicleAsync(HttpClient client, Guid customerId, string seed)
    {
        var brandName = $"E2E WO Brand {seed[..8]}";
        using var createBrandResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new CreateVehicleBrandRequest(brandName));

        HttpResponseAssertions.AssertStatus(createBrandResponse, HttpStatusCode.Created);
        var createdBrand = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleBrandResponse>(createBrandResponse);

        var modelName = $"E2E WO Model {seed[..8]}";
        using var createModelResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new CreateVehicleModelRequest(createdBrand.Id, modelName));

        HttpResponseAssertions.AssertStatus(createModelResponse, HttpStatusCode.Created);
        var createdModel = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleModelResponse>(createModelResponse);

        var colorName = $"E2E WO Color {seed[..8]}";
        using var createColorResponse = await client.PostAsJsonAsync(
            "/vehicle-colors",
            new CreateVehicleColorRequest(colorName));

        HttpResponseAssertions.AssertStatus(createColorResponse, HttpStatusCode.Created);
        var createdColor = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleColorResponse>(createColorResponse);

        var plate = GenerateMercosulPlate(seed);
        using var createVehicleResponse = await client.PostAsJsonAsync(
            "/vehicles",
            new CreateVehicleRequest(
                plate,
                2025,
                customerId,
                createdModel.Id,
                createdColor.Id));

        HttpResponseAssertions.AssertStatus(createVehicleResponse, HttpStatusCode.Created);
        var createdVehicle = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleResponse>(createVehicleResponse);
        Assert.Equal(customerId, createdVehicle.CustomerId);
        Assert.Equal(createdBrand.Id, createdVehicle.VehicleBrandId);
        Assert.Equal(createdModel.Id, createdVehicle.VehicleModelId);
        Assert.Equal(createdColor.Id, createdVehicle.VehicleColorId);
        Assert.Equal(plate, createdVehicle.Plate);

        return new CreatedVehicleDependencies(createdVehicle.Id);
    }

    private static async Task<ServiceResponse> CreateServiceAsync(HttpClient client, string seed)
    {
        var request = new CreateServiceRequest(
            Description: $"E2E WO Service {seed[..8]}",
            Price: 189.90m);

        using var response = await client.PostAsJsonAsync("/services", request);
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(request.Description, payload.Description);
        Assert.Equal(request.Price, payload.Price);
        Assert.NotEqual(default, payload.CreatedAt);
        return payload;
    }

    private async Task<InventoryItemResponse> CreateInventoryItemAsync(string seed)
    {
        using var attendantClient = await _fixture.CreateAuthenticatedClientAsync();
        _ = await attendantClient.AuthenticateAsActiveAttendantAsync(seed);

        var request = new CreateInventoryItemRequest(
            Name: $"E2E WO Item {seed[..8]}",
            Description: $"E2E WO Item Description {seed[..8]}",
            Type: "Part",
            Cost: 50m,
            Price: 95m,
            StockQuantity: 12);

        using var response = await attendantClient.PostAsJsonAsync("/inventory-items", request);
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(request.Name, payload.Name);
        Assert.Equal(request.Description, payload.Description);
        Assert.Equal(request.Type, payload.Type);
        Assert.Equal(request.Cost, payload.Cost);
        Assert.Equal(request.Price, payload.Price);
        Assert.Equal(request.StockQuantity, payload.StockQuantity);
        Assert.NotEqual(default, payload.CreatedAt);

        return payload;
    }

    private async Task<HttpClient> CreateAuthenticatedCustomerPortalClientAsync(
        HttpClient staffClient,
        Guid customerId,
        string seed)
    {
        var birthDate = new DateOnly(1991, 1, 11);
        using var activatePortalUserResponse = await staffClient.PostAsJsonAsync(
            $"/customers/{customerId}/portal-user",
            new ActivateCustomerPortalUserRequest(birthDate));

        HttpResponseAssertions.AssertStatus(activatePortalUserResponse, HttpStatusCode.Created);
        var activatedUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activatePortalUserResponse);

        var customerClient = _fixture.CreateClient();
        try
        {
            var initialPassword = GenerateInitialPassword(activatedUser.FullName, activatedUser.BirthDate);
            var firstLogin = await customerClient.LoginAndAttachBearerTokenAsync(activatedUser.Email, initialPassword);
            Assert.True(firstLogin.MustChangePassword);

            var activePassword = $"E2E.Customer.Active#{seed[..10]}";
            using var changePasswordResponse = await customerClient.PutAsJsonAsync(
                "/users/me/password",
                new ChangeMyPasswordRequest(initialPassword, activePassword));

            HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);

            var activeLogin = await customerClient.LoginAndAttachBearerTokenAsync(activatedUser.Email, activePassword);
            Assert.False(activeLogin.MustChangePassword);

            return customerClient;
        }
        catch
        {
            customerClient.Dispose();
            throw;
        }
    }

    private static string GenerateInitialPassword(string fullName, DateOnly birthDate)
    {
        var lastName = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].ToLowerInvariant();
        return $"{lastName}{birthDate.Year}";
    }

    private static string GeneratePhoneNumber(string seed)
    {
        var hash = Math.Abs(seed.GetHashCode(StringComparison.Ordinal));
        return $"11{hash % 1_000_000_000:D9}";
    }

    private static string GenerateValidCpf(string seed)
    {
        var seedDigits = seed
            .Select(static character => char.IsDigit(character) ? character - '0' : char.ToUpperInvariant(character) % 10)
            .ToArray();

        var baseDigits = new int[9];
        for (var index = 0; index < baseDigits.Length; index++)
        {
            baseDigits[index] = seedDigits[index % seedDigits.Length];
        }

        if (baseDigits.Distinct().Count() == 1)
        {
            baseDigits[8] = (baseDigits[8] + 1) % 10;
        }

        var firstDigit = CalculateCpfCheckDigit(baseDigits, 10);
        var firstTenDigits = new int[10];
        Array.Copy(baseDigits, firstTenDigits, baseDigits.Length);
        firstTenDigits[9] = firstDigit;
        var secondDigit = CalculateCpfCheckDigit(firstTenDigits, 11);

        var cpfDigits = new char[11];
        for (var index = 0; index < baseDigits.Length; index++)
        {
            cpfDigits[index] = (char)('0' + baseDigits[index]);
        }

        cpfDigits[9] = (char)('0' + firstDigit);
        cpfDigits[10] = (char)('0' + secondDigit);
        return new string(cpfDigits);
    }

    private static int CalculateCpfCheckDigit(int[] digits, int startWeight)
    {
        var sum = 0;
        for (var index = 0; index < digits.Length; index++)
        {
            sum += digits[index] * (startWeight - index);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static string GenerateMercosulPlate(string seed)
    {
        var normalized = seed.Trim().ToUpperInvariant().PadRight(7, '0');
        var plate = new char[7];
        plate[0] = HexToLetter(normalized[0]);
        plate[1] = HexToLetter(normalized[1]);
        plate[2] = HexToLetter(normalized[2]);
        plate[3] = HexToDigit(normalized[3]);
        plate[4] = HexToLetter(normalized[4]);
        plate[5] = HexToDigit(normalized[5]);
        plate[6] = HexToDigit(normalized[6]);
        return new string(plate);
    }

    private static char HexToLetter(char value)
    {
        return (char)('A' + (HexToInt(value) % 26));
    }

    private static char HexToDigit(char value)
    {
        return (char)('0' + (HexToInt(value) % 10));
    }

    private static int HexToInt(char value)
    {
        if (value is >= '0' and <= '9')
        {
            return value - '0';
        }

        if (value is >= 'A' and <= 'F')
        {
            return (value - 'A') + 10;
        }

        return Math.Abs(value.GetHashCode()) % 16;
    }

    private sealed record WorkOrderSetupIds(
        Guid CustomerId,
        Guid VehicleId,
        Guid ServiceId,
        Guid InventoryItemId,
        string ServiceDescription,
        decimal ServicePrice,
        string InventoryItemDescription,
        decimal InventoryItemCost,
        decimal InventoryItemPrice);

    private sealed record WorkOrderQueueSeed(
        Guid CurrentId,
        Guid SeededId,
        string Status,
        DateTime CreatedAt);

    private sealed record ListWorkOrdersResponse(
        IReadOnlyList<WorkOrderDetailsResponse> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record PaginatedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record CreatedVehicleDependencies(Guid Id);

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

    private sealed record AddEstimateServiceRequest(Guid ServiceId);

    private sealed record AddEstimateServiceResponse(
        Guid EstimateId,
        Guid ServiceId,
        string Description,
        decimal UnitPrice,
        decimal TotalPrice);

    private sealed record AddEstimateInventoryItemRequest(Guid InventoryItemId, int Quantity);

    private sealed record AddEstimateInventoryItemResponse(
        Guid EstimateId,
        Guid InventoryItemId,
        string Description,
        int Quantity,
        decimal UnitCost,
        decimal UnitPrice,
        decimal TotalPrice);

    private sealed record WorkOrderDetailsResponse(
        Guid Id,
        Guid CustomerId,
        Guid VehicleId,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<WorkOrderEstimateResponse> Estimates);

    private sealed record WorkOrderEstimateResponse(
        Guid Id,
        Guid WorkOrderId,
        string Status,
        decimal TotalAmount,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<WorkOrderInventoryLineResponse> InventoryLines,
        IReadOnlyList<WorkOrderServiceLineResponse> ServiceLines);

    private sealed record WorkOrderInventoryLineResponse(
        Guid Id,
        Guid EstimateId,
        Guid InventoryItemId,
        string Description,
        int Quantity,
        decimal UnitCost,
        decimal UnitPrice,
        decimal TotalPrice);

    private sealed record WorkOrderServiceLineResponse(
        Guid Id,
        Guid EstimateId,
        Guid ServiceId,
        string Description,
        decimal UnitPrice,
        decimal TotalPrice,
        string Status,
        DateTime? StartedAt,
        DateTime? CompletedAt);

    private sealed record CreateCustomerRequest(
        string TaxDocument,
        string FullName,
        string Email,
        string PhoneNumber);

    private sealed record CreateCustomerResponse(
        Guid Id,
        string TaxDocument,
        string TaxDocumentType,
        string FullName,
        string Email,
        string PhoneNumber,
        DateTime CreatedAt);

    private sealed record CreateVehicleBrandRequest(string Name);

    private sealed record CreateVehicleBrandResponse(
        Guid Id,
        string Name,
        DateTime CreatedAt);

    private sealed record CreateVehicleModelRequest(Guid VehicleBrandId, string Name);

    private sealed record CreateVehicleModelResponse(
        Guid Id,
        Guid VehicleBrandId,
        string Name,
        DateTime CreatedAt);

    private sealed record CreateVehicleColorRequest(string Name);

    private sealed record CreateVehicleColorResponse(
        Guid Id,
        string Name,
        DateTime CreatedAt);

    private sealed record CreateVehicleRequest(
        string Plate,
        int Year,
        Guid CustomerId,
        Guid VehicleModelId,
        Guid VehicleColorId);

    private sealed record CreateVehicleResponse(
        Guid Id,
        Guid CustomerId,
        int Year,
        Guid VehicleBrandId,
        Guid VehicleModelId,
        Guid VehicleColorId,
        string Plate,
        DateTime CreatedAt);

    private sealed record CreateServiceRequest(string Description, decimal Price);

    private sealed record ServiceResponse(
        Guid Id,
        string Description,
        decimal Price,
        DateTime CreatedAt);

    private sealed record CreateInventoryItemRequest(
        string Name,
        string Description,
        string Type,
        decimal Cost,
        decimal Price,
        int StockQuantity);

    private sealed record InventoryItemResponse(
        Guid Id,
        string Name,
        string Description,
        string Type,
        decimal Cost,
        decimal Price,
        int StockQuantity,
        DateTime CreatedAt);

    private sealed record ActivateCustomerPortalUserRequest(DateOnly BirthDate);

    private sealed record ActivateCustomerPortalUserResponse(
        Guid Id,
        Guid CustomerId,
        string FullName,
        string Email,
        DateOnly BirthDate,
        string Role,
        bool MustChangePassword,
        DateTime CreatedAt);

    private sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);
}
