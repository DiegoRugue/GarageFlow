using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using System.Reflection;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Api.WorkOrders.GetAverageServiceTime;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Tests.Integration.Api.Customers.Contracts;
using GarageFlow.Tests.Integration.Api.InventoryItems.Contracts;
using GarageFlow.Tests.Integration.Api.Services.Contracts;
using GarageFlow.Tests.Integration.Api.Users.Contracts;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.InventoryItems;
using GarageFlow.Tests.Shared.Services;
using GarageFlow.Tests.Shared.Users;
using GarageFlow.Tests.Shared.WorkOrders;

namespace GarageFlow.Tests.Integration.Api.WorkOrders;

public class WorkOrdersApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task AverageServiceTime_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AverageServiceTime_ShouldReturn403_WhenAuthenticatedUserIsCustomer()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var activateResponse = await client.PostAsJsonAsync(
            $"/customers/{seededVehicle.CustomerId}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1991, 1, 11)));
        HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
        var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

        await AuthenticateCreatedUserAsActiveAsync(
            client,
            customerUser.Email,
            customerUser.FullName,
            customerUser.BirthDate,
            "Customer.Average.Service.Time#123");

        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);

        var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AverageServiceTime_ShouldReturn400_WhenWindowIsInvalid()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var from = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, from));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AverageServiceTime_ShouldReturn400_WhenServiceIdIsEmptyGuid()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to, Guid.Empty));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AverageServiceTime_ShouldReturnNullAverage_WhenWindowHasNoCompletedServices()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var from = DateTime.UtcNow.AddDays(365);
        var to = DateTime.UtcNow.AddDays(366);

        var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<AverageServiceTimeResponse>(response);
        Assert.Equal(0, payload.CompletedServicesCount);
        Assert.Null(payload.ServiceId);
        Assert.Null(payload.AverageDurationMinutes);
    }

    [Fact]
    public async Task AverageServiceTime_ShouldReturnAverageDuration_ForCompletedServiceLinesInWindow()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var workOrder = await CreateWorkOrderAsync(client, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var estimate = await CreateEstimateAsync(client, workOrder.Id);
        var service = await CreateServiceAsync(
            client,
            new ServiceBuilder()
                .WithDescription($"Average service labor {Guid.NewGuid():N}")
                .WithPrice(150m));

        var addServiceResponse = await client.PostAsJsonAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services",
            new AddEstimateServiceRequest(service.Id));
        HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

        var submitResponse = await client.PostAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/submit",
            content: null);
        HttpResponseAssertions.AssertStatus(submitResponse, HttpStatusCode.NoContent);

        var activateResponse = await client.PostAsJsonAsync(
            $"/customers/{seededVehicle.CustomerId}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1991, 1, 11)));
        HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
        var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

        await AuthenticateCreatedUserAsActiveAsync(
            client,
            customerUser.Email,
            customerUser.FullName,
            customerUser.BirthDate,
            "Customer.Average.Approve#123");

        var approveResponse = await client.PostAsync(
            $"/me/work-orders/{workOrder.Id}/estimates/{estimate.Id}/approve",
            content: null);
        HttpResponseAssertions.AssertStatus(approveResponse, HttpStatusCode.NoContent);

        await client.AuthenticateAsActiveBootstrapAdminAsync();
        var approvedDetails = await GetWorkOrderDetailsAsync(client, workOrder.Id);
        var serviceLineId = Assert.Single(approvedDetails.Estimates.Single().ServiceLines).Id;

        var from = DateTime.UtcNow.AddMinutes(-1);
        var startResponse = await client.PostAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{serviceLineId}/start",
            content: null);
        HttpResponseAssertions.AssertStatus(startResponse, HttpStatusCode.NoContent);

        var completeResponse = await client.PostAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{serviceLineId}/complete",
            content: null);
        HttpResponseAssertions.AssertStatus(completeResponse, HttpStatusCode.NoContent);
        var to = DateTime.UtcNow.AddMinutes(1);

        var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to, service.Id));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<AverageServiceTimeResponse>(response);
        Assert.Equal(service.Id, payload.ServiceId);
        Assert.Equal(1, payload.CompletedServicesCount);
        Assert.NotNull(payload.AverageDurationMinutes);
        Assert.True(payload.AverageDurationMinutes >= 0);
    }

    [Fact]
    public async Task AverageServiceTime_ShouldNormalizeOffsetWindowToUtc()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var from = new DateTimeOffset(2026, 5, 1, 3, 0, 0, TimeSpan.FromHours(3));
        var to = new DateTimeOffset(2026, 5, 1, 4, 0, 0, TimeSpan.FromHours(3));

        var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<AverageServiceTimeResponse>(response);
        Assert.Equal(from.UtcDateTime, payload.From);
        Assert.Equal(to.UtcDateTime, payload.To);
        Assert.Equal(DateTimeKind.Utc, payload.From.Kind);
        Assert.Equal(DateTimeKind.Utc, payload.To.Kind);
    }

    [Fact]
    public void AverageServiceTime_EndpointBinding_ShouldUseDateTimeOffsetWindow()
    {
        var endpointMethod = typeof(GetAverageServiceTimeEndpoint).GetMethod(
            "GetAverageServiceTime",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(endpointMethod);

        var parameters = endpointMethod.GetParameters();
        Assert.Equal(typeof(DateTimeOffset), parameters[0].ParameterType);
        Assert.Equal(typeof(DateTimeOffset), parameters[1].ParameterType);
        Assert.Equal(typeof(Guid?), parameters[2].ParameterType);
    }

    [Fact]
    public async Task WorkOrdersStaffRoutes_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/work-orders?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartEstimateService_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.PostAsync(
            $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/start",
            content: null);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteEstimateService_ShouldReturn401_WhenRequestHasNoToken()
    {
        using var client = _fixture.CreateClient();

        var response = await client.PostAsync(
            $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/complete",
            content: null);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartEstimateService_ShouldReturn403_WhenAuthenticatedUserIsCustomer()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            seededVehicle.CustomerId,
            "Customer.Start.Service.Forbidden#123");

        var response = await customerClient.PostAsync(
            $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/start",
            content: null);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompleteEstimateService_ShouldReturn403_WhenAuthenticatedUserIsCustomer()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            seededVehicle.CustomerId,
            "Customer.Complete.Service.Forbidden#123");

        var response = await customerClient.PostAsync(
            $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/complete",
            content: null);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_ShouldStartAndCompleteEstimateService_AndSeeServiceExecutionDetails()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var workOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var estimate = await CreateEstimateAsync(staffClient, workOrder.Id);
        var service = await CreateServiceAsync(
            staffClient,
            new ServiceBuilder()
                .WithDescription($"Execution service {Guid.NewGuid():N}")
                .WithPrice(125m));

        var addServiceResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services",
            new AddEstimateServiceRequest(service.Id));
        HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

        await SubmitAndApproveEstimateAsync(_fixture, staffClient, seededVehicle.CustomerId, workOrder.Id, estimate.Id);

        var approvedDetails = await GetWorkOrderDetailsAsync(staffClient, workOrder.Id);
        var serviceLineId = Assert.Single(approvedDetails.Estimates.Single().ServiceLines).Id;

        var startResponse = await staffClient.PostAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{serviceLineId}/start",
            content: null);
        HttpResponseAssertions.AssertStatus(startResponse, HttpStatusCode.NoContent);

        var inProgressDetails = await GetWorkOrderDetailsAsync(staffClient, workOrder.Id);
        var inProgressService = Assert.Single(inProgressDetails.Estimates.Single().ServiceLines);
        Assert.Equal("InProgress", inProgressService.Status);
        Assert.NotNull(inProgressService.StartedAt);
        Assert.Null(inProgressService.CompletedAt);
        Assert.Equal("InProgress", inProgressDetails.Status);

        var completeResponse = await staffClient.PostAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{serviceLineId}/complete",
            content: null);
        HttpResponseAssertions.AssertStatus(completeResponse, HttpStatusCode.NoContent);

        var completedDetails = await GetWorkOrderDetailsAsync(staffClient, workOrder.Id);
        var completedService = Assert.Single(completedDetails.Estimates.Single().ServiceLines);
        Assert.Equal("Completed", completedService.Status);
        Assert.NotNull(completedService.StartedAt);
        Assert.NotNull(completedService.CompletedAt);
        Assert.Equal("Completed", completedDetails.Status);
    }

    [Fact]
    public async Task Staff_ShouldCreateWorkOrder_WithCreatedStatus()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var created = await CreateWorkOrderAsync(client, seededVehicle.CustomerId, seededVehicle.VehicleId);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Created", created.Status);
        Assert.Equal(seededVehicle.CustomerId, created.CustomerId);
        Assert.Equal(seededVehicle.VehicleId, created.VehicleId);
    }

    [Fact]
    public async Task Staff_ShouldReceive409_WhenVehicleBelongsToAnotherCustomer()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var firstCustomerVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var secondCustomerVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            client,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var response = await client.PostAsJsonAsync(
            "/work-orders",
            new CreateWorkOrderRequest(firstCustomerVehicle.CustomerId, secondCustomerVehicle.VehicleId));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CompleteWorkOrder_ShouldReturn404_WhenDirectCompletionRouteIsRemoved()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var workOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);

        var response = await staffClient.PostAsync($"/work-orders/{workOrder.Id}/complete", content: null);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Staff_ShouldAddInventoryItem_AndDecreaseStock()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var createdWorkOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var createdEstimate = await CreateEstimateAsync(staffClient, createdWorkOrder.Id);
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName($"Alternator-{Guid.NewGuid():N}")
                .WithDescription("Alternator replacement unit")
                .WithCost(180m)
                .WithPrice(320m)
                .WithStockQuantity(10));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 3));

        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        var inventoryDetailsResponse = await attendantClient.GetAsync($"/inventory-items/{inventoryItem.Id}");
        HttpResponseAssertions.AssertStatus(inventoryDetailsResponse, HttpStatusCode.OK);
        var inventoryDetails = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(inventoryDetailsResponse);
        Assert.Equal(7, inventoryDetails.StockQuantity);
    }

    [Fact]
    public async Task Staff_ShouldReceive409_WhenInventoryStockIsInsufficient()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var createdWorkOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var createdEstimate = await CreateEstimateAsync(staffClient, createdWorkOrder.Id);
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName($"Fuse-{Guid.NewGuid():N}")
                .WithDescription("Electrical fuse")
                .WithCost(5m)
                .WithPrice(12m)
                .WithStockQuantity(1));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 2));

        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.Conflict);

        var inventoryDetailsResponse = await attendantClient.GetAsync($"/inventory-items/{inventoryItem.Id}");
        HttpResponseAssertions.AssertStatus(inventoryDetailsResponse, HttpStatusCode.OK);
        var inventoryDetails = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(inventoryDetailsResponse);
        Assert.Equal(1, inventoryDetails.StockQuantity);
    }

    [Fact]
    public async Task Staff_ShouldReceive409_WhenSubmittingEmptyEstimate()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var createdWorkOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var createdEstimate = await CreateEstimateAsync(staffClient, createdWorkOrder.Id);

        var submitResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/submit",
            content: null);

        HttpResponseAssertions.AssertStatus(submitResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Staff_ShouldReceive409_WhenSubmittingInventoryOnlyEstimate()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);
        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var createdWorkOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var createdEstimate = await CreateEstimateAsync(staffClient, createdWorkOrder.Id);
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName($"InventoryOnly-{Guid.NewGuid():N}")
                .WithDescription("Inventory-only line")
                .WithCost(40m)
                .WithPrice(80m)
                .WithStockQuantity(6));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 1));
        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        var submitResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/submit",
            content: null);

        HttpResponseAssertions.AssertStatus(submitResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customer_ShouldListOnlyOwnWorkOrders()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var firstSeededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var secondSeededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var firstWorkOrder = await CreateWorkOrderAsync(staffClient, firstSeededVehicle.CustomerId, firstSeededVehicle.VehicleId);
        var secondWorkOrder = await CreateWorkOrderAsync(staffClient, secondSeededVehicle.CustomerId, secondSeededVehicle.VehicleId);

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            firstSeededVehicle.CustomerId,
            "Customer.List.Own#123");

        var response = await customerClient.GetAsync("/me/work-orders?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<CustomerWorkOrderDetailsResponse>>(response);
        Assert.Contains(payload.Items, item => item.Id == firstWorkOrder.Id);
        Assert.DoesNotContain(payload.Items, item => item.Id == secondWorkOrder.Id);
    }

    [Fact]
    public async Task Customer_ShouldReceive404_WhenAccessingAnotherCustomersWorkOrder()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        var firstSeededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var secondSeededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        _ = await CreateWorkOrderAsync(staffClient, firstSeededVehicle.CustomerId, firstSeededVehicle.VehicleId);
        var secondWorkOrder = await CreateWorkOrderAsync(staffClient, secondSeededVehicle.CustomerId, secondSeededVehicle.VehicleId);

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            firstSeededVehicle.CustomerId,
            "Customer.Get.Other#123");

        var response = await customerClient.GetAsync($"/me/work-orders/{secondWorkOrder.Id}");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customer_ShouldApproveOwnPendingEstimate()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var workOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var estimate = await CreateEstimateAsync(staffClient, workOrder.Id);
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName($"Battery-{Guid.NewGuid():N}")
                .WithDescription("Car battery")
                .WithCost(210m)
                .WithPrice(380m)
                .WithStockQuantity(5));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 1));
        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        var service = await CreateServiceAsync(
            staffClient,
            new ServiceBuilder()
                .WithDescription($"Approve own service {Guid.NewGuid():N}")
                .WithPrice(150m));
        var addServiceResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services",
            new AddEstimateServiceRequest(service.Id));
        HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

        var submitEstimateResponse = await staffClient.PostAsync(
            $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/submit",
            content: null);
        HttpResponseAssertions.AssertStatus(submitEstimateResponse, HttpStatusCode.NoContent);

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            seededVehicle.CustomerId,
            "Customer.Approve.Own#123");

        var approveResponse = await customerClient.PostAsync(
            $"/me/work-orders/{workOrder.Id}/estimates/{estimate.Id}/approve",
            content: null);
        HttpResponseAssertions.AssertStatus(approveResponse, HttpStatusCode.NoContent);

        var detailsResponse = await customerClient.GetAsync($"/me/work-orders/{workOrder.Id}");
        HttpResponseAssertions.AssertStatus(detailsResponse, HttpStatusCode.OK);
        var details = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerWorkOrderDetailsResponse>(detailsResponse);
        Assert.Equal("Approved", details.Status);
        Assert.Contains(details.Estimates, item => item.Id == estimate.Id && item.Status == "Approved");
    }

    [Fact]
    public async Task Customer_ShouldReceive404_WhenApprovingAnotherCustomersEstimate()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var firstSeededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var secondSeededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        _ = await CreateWorkOrderAsync(staffClient, firstSeededVehicle.CustomerId, firstSeededVehicle.VehicleId);

        var secondWorkOrder = await CreateWorkOrderAsync(staffClient, secondSeededVehicle.CustomerId, secondSeededVehicle.VehicleId);
        var secondEstimate = await CreateEstimateAsync(staffClient, secondWorkOrder.Id);
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName($"Coil-{Guid.NewGuid():N}")
                .WithDescription("Ignition coil")
                .WithCost(90m)
                .WithPrice(170m)
                .WithStockQuantity(8));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{secondWorkOrder.Id}/estimates/{secondEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 2));
        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        var service = await CreateServiceAsync(
            staffClient,
            new ServiceBuilder()
                .WithDescription($"Approve other service {Guid.NewGuid():N}")
                .WithPrice(95m));
        var addServiceResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{secondWorkOrder.Id}/estimates/{secondEstimate.Id}/services",
            new AddEstimateServiceRequest(service.Id));
        HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

        var submitEstimateResponse = await staffClient.PostAsync(
            $"/work-orders/{secondWorkOrder.Id}/estimates/{secondEstimate.Id}/submit",
            content: null);
        HttpResponseAssertions.AssertStatus(submitEstimateResponse, HttpStatusCode.NoContent);

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            firstSeededVehicle.CustomerId,
            "Customer.Approve.Other#123");

        var response = await customerClient.PostAsync(
            $"/me/work-orders/{secondWorkOrder.Id}/estimates/{secondEstimate.Id}/approve",
            content: null);

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);

        var targetDetailsResponse = await staffClient.GetAsync($"/work-orders/{secondWorkOrder.Id}");
        HttpResponseAssertions.AssertStatus(targetDetailsResponse, HttpStatusCode.OK);
        var targetDetails = await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(targetDetailsResponse);
        Assert.Equal("WaitingApproval", targetDetails.Status);
        Assert.Contains(targetDetails.Estimates, estimate => estimate.Id == secondEstimate.Id && estimate.Status == "Pending");
    }

    [Fact]
    public async Task Customer_ShouldRejectOwnPendingEstimate_AndRestoreStock()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());
        var createdWorkOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var createdEstimate = await CreateEstimateAsync(staffClient, createdWorkOrder.Id);

        const int originalStock = 12;
        const int reservedQuantity = 4;
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName($"Timing-Belt-{Guid.NewGuid():N}")
                .WithDescription("Timing belt replacement part")
                .WithCost(120m)
                .WithPrice(220m)
                .WithStockQuantity(originalStock));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: reservedQuantity));
        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        var service = await CreateServiceAsync(
            staffClient,
            new ServiceBuilder()
                .WithDescription($"Reject own service {Guid.NewGuid():N}")
                .WithPrice(210m));
        var addServiceResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/services",
            new AddEstimateServiceRequest(service.Id));
        HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

        var stockAfterReservationResponse = await attendantClient.GetAsync($"/inventory-items/{inventoryItem.Id}");
        HttpResponseAssertions.AssertStatus(stockAfterReservationResponse, HttpStatusCode.OK);
        var stockAfterReservation = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(stockAfterReservationResponse);
        Assert.Equal(originalStock - reservedQuantity, stockAfterReservation.StockQuantity);

        var submitEstimateResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/submit",
            content: null);
        HttpResponseAssertions.AssertStatus(submitEstimateResponse, HttpStatusCode.NoContent);

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            seededVehicle.CustomerId,
            "Customer.Reject.Own#123");

        var rejectResponse = await customerClient.PostAsync(
            $"/me/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/reject",
            content: null);
        HttpResponseAssertions.AssertStatus(rejectResponse, HttpStatusCode.NoContent);

        var stockAfterRejectResponse = await attendantClient.GetAsync($"/inventory-items/{inventoryItem.Id}");
        HttpResponseAssertions.AssertStatus(stockAfterRejectResponse, HttpStatusCode.OK);
        var stockAfterReject = await HttpResponseAssertions.ReadRequiredJsonAsync<InventoryItemResponse>(stockAfterRejectResponse);
        Assert.Equal(originalStock, stockAfterReject.StockQuantity);

        var detailsResponse = await customerClient.GetAsync($"/me/work-orders/{createdWorkOrder.Id}");
        HttpResponseAssertions.AssertStatus(detailsResponse, HttpStatusCode.OK);
        var details = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerWorkOrderDetailsResponse>(detailsResponse);
        Assert.Contains(details.Estimates, item => item.Id == createdEstimate.Id && item.Status == "Rejected");
    }

    [Fact]
    public async Task CustomerWorkOrderResponse_ShouldNotExposeInventoryCost()
    {
        using var staffClient = await _fixture.CreateAuthenticatedClientAsync();
        using var attendantClient = await CreateAuthenticatedAttendantClientAsync(_fixture);

        var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
            staffClient,
            customerBuilder: CustomerSeed.CreateUniqueBuilder());

        var createdWorkOrder = await CreateWorkOrderAsync(staffClient, seededVehicle.CustomerId, seededVehicle.VehicleId);
        var createdEstimate = await CreateEstimateAsync(staffClient, createdWorkOrder.Id);
        var inventoryItem = await CreateInventoryItemAsync(
            attendantClient,
            new InventoryItemBuilder()
                .WithName($"Water-Pump-{Guid.NewGuid():N}")
                .WithDescription("Water pump replacement part")
                .WithCost(140m)
                .WithPrice(260m)
                .WithStockQuantity(30));

        var addInventoryResponse = await staffClient.PostAsJsonAsync(
            $"/work-orders/{createdWorkOrder.Id}/estimates/{createdEstimate.Id}/inventory-items",
            new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 1));
        HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            _fixture,
            staffClient,
            seededVehicle.CustomerId,
            "Customer.UnitCost.Active#123");

        var listResponse = await customerClient.GetAsync("/me/work-orders?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync();

        using var listJson = JsonDocument.Parse(listBody);
        var listItems = listJson.RootElement.GetProperty("items");
        var matchingWorkOrder = listItems
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == createdWorkOrder.Id);
        var matchingEstimate = matchingWorkOrder
            .GetProperty("estimates")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == createdEstimate.Id);
        _ = matchingEstimate
            .GetProperty("inventoryLines")
            .EnumerateArray()
            .Single(item => item.GetProperty("inventoryItemId").GetGuid() == inventoryItem.Id);

        AssertDoesNotExposeCostFields(listBody);

        var detailsResponse = await customerClient.GetAsync($"/me/work-orders/{createdWorkOrder.Id}");
        HttpResponseAssertions.AssertStatus(detailsResponse, HttpStatusCode.OK);
        var detailsBody = await detailsResponse.Content.ReadAsStringAsync();
        AssertDoesNotExposeCostFields(detailsBody);

        await using var bodyStream = await detailsResponse.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(bodyStream);
        var inventoryLine = json.RootElement
            .GetProperty("estimates")[0]
            .GetProperty("inventoryLines")[0];

        Assert.True(inventoryLine.TryGetProperty("unitPrice", out _));
        Assert.True(inventoryLine.TryGetProperty("totalPrice", out _));
        Assert.False(inventoryLine.TryGetProperty("unitCost", out _));
    }

    private static async Task<HttpClient> CreateAuthenticatedCustomerClientAsync(
        GarageFlowApiFixture fixture,
        HttpClient staffClient,
        Guid customerId,
        string newPassword)
    {
        var activateResponse = await staffClient.PostAsJsonAsync(
            $"/customers/{customerId}/portal-user",
            new ActivateCustomerPortalUserRequest(new DateOnly(1991, 1, 11)));
        HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
        var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

        var customerClient = fixture.CreateClient();
        await AuthenticateCreatedUserAsActiveAsync(
            customerClient,
            customerUser.Email,
            customerUser.FullName,
            customerUser.BirthDate,
            newPassword);

        return customerClient;
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

    private static async Task SubmitAndApproveEstimateAsync(
        GarageFlowApiFixture fixture,
        HttpClient staffClient,
        Guid customerId,
        Guid workOrderId,
        Guid estimateId)
    {
        var submitResponse = await staffClient.PostAsync(
            $"/work-orders/{workOrderId}/estimates/{estimateId}/submit",
            content: null);
        HttpResponseAssertions.AssertStatus(submitResponse, HttpStatusCode.NoContent);

        using var customerClient = await CreateAuthenticatedCustomerClientAsync(
            fixture,
            staffClient,
            customerId,
            "Customer.Submit.Approve#123");

        var approveResponse = await customerClient.PostAsync(
            $"/me/work-orders/{workOrderId}/estimates/{estimateId}/approve",
            content: null);
        HttpResponseAssertions.AssertStatus(approveResponse, HttpStatusCode.NoContent);
    }

    private static async Task<WorkOrderDetailsResponse> GetWorkOrderDetailsAsync(HttpClient client, Guid workOrderId)
    {
        var response = await client.GetAsync($"/work-orders/{workOrderId}");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(response);
    }

    private static async Task<CreateWorkOrderResponse> CreateWorkOrderAsync(HttpClient client, Guid customerId, Guid vehicleId)
    {
        var response = await client.PostAsJsonAsync("/work-orders", new CreateWorkOrderRequest(customerId, vehicleId));
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        Assert.NotNull(response.Headers.Location);
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

    private static async Task<ServiceResponse> CreateServiceAsync(HttpClient client, ServiceBuilder? builder = null)
    {
        var request = (builder ?? new ServiceBuilder()).BuildCreateRequest();
        var response = await client.PostAsJsonAsync("/services", request);
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(response);
    }

    private static string CreateAverageServiceTimeUrl(DateTime from, DateTime to)
    {
        return CreateAverageServiceTimeUrl(
            new DateTimeOffset(from.ToUniversalTime(), TimeSpan.Zero),
            new DateTimeOffset(to.ToUniversalTime(), TimeSpan.Zero));
    }

    private static string CreateAverageServiceTimeUrl(DateTime from, DateTime to, Guid? serviceId)
    {
        return CreateAverageServiceTimeUrl(
            new DateTimeOffset(from.ToUniversalTime(), TimeSpan.Zero),
            new DateTimeOffset(to.ToUniversalTime(), TimeSpan.Zero),
            serviceId);
    }

    private static string CreateAverageServiceTimeUrl(DateTimeOffset from, DateTimeOffset to, Guid? serviceId = null)
    {
        var serviceIdFilter = serviceId.HasValue ? $"&serviceId={serviceId.Value}" : string.Empty;
        return $"/work-orders/average-service-time?from={FormatOffset(from)}&to={FormatOffset(to)}{serviceIdFilter}";
    }

    private static string FormatOffset(DateTimeOffset value)
    {
        return Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void AssertDoesNotExposeCostFields(string body)
    {
        Assert.DoesNotContain("unitCost", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totalCost", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"cost\"", body, StringComparison.OrdinalIgnoreCase);
    }

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
}
