using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Contracts.Auth;
using GarageFlow.Tests.E2E.Support.Contracts.Common;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.WorkOrders;

public sealed class WorkOrdersE2eTests(E2eApiFixture fixture) : IClassFixture<E2eApiFixture>
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

        // Then the work order starts in the Created state.
        HttpResponseAssertions.AssertStatus(createWorkOrderResponse, HttpStatusCode.Created);
        Assert.NotNull(createWorkOrderResponse.Headers.Location);

        var createdWorkOrder = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateWorkOrderResponse>(createWorkOrderResponse);
        Assert.NotEqual(Guid.Empty, createdWorkOrder.Id);
        Assert.Equal(setup.CustomerId, createdWorkOrder.CustomerId);
        Assert.Equal(setup.VehicleId, createdWorkOrder.VehicleId);
        Assert.Equal("Created", createdWorkOrder.Status);
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

        // Then the approval is accepted and the work order becomes approved.
        HttpResponseAssertions.AssertStatus(approveEstimateResponse, HttpStatusCode.NoContent);

        using var detailsAfterApprovalResponse = await staffClient.GetAsync($"/work-orders/{createdWorkOrder.Id}");
        HttpResponseAssertions.AssertStatus(detailsAfterApprovalResponse, HttpStatusCode.OK);
        var detailsAfterApproval = await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(detailsAfterApprovalResponse);
        Assert.Equal("Approved", detailsAfterApproval.Status);

        var estimateAfterApproval = Assert.Single(detailsAfterApproval.Estimates);
        Assert.Equal(createdEstimate.Id, estimateAfterApproval.Id);
        Assert.Equal("Approved", estimateAfterApproval.Status);
        var approvedServiceLine = Assert.Single(estimateAfterApproval.ServiceLines);
        Assert.Equal("Pending", approvedServiceLine.Status);
        Assert.Null(approvedServiceLine.StartedAt);
        Assert.Null(approvedServiceLine.CompletedAt);

        // When staff starts the work order and completes the approved service line.
        using var startWorkResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/start-work",
            content: null);

        HttpResponseAssertions.AssertStatus(startWorkResponse, HttpStatusCode.NoContent);

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
    public async Task WorkOrders_ShouldReturnConflict_ForInvalidTransition()
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

        // When staff tries to start work before customer approval.
        using var invalidTransitionResponse = await staffClient.PostAsync(
            $"/work-orders/{createdWorkOrder.Id}/start-work",
            content: null);

        // Then the domain state machine is exposed as a conflict ProblemDetails response.
        HttpResponseAssertions.AssertStatus(invalidTransitionResponse, HttpStatusCode.Conflict);

        var problem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(invalidTransitionResponse);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.Equal((int)HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ConflictProblemTitle, problem.Title);
        Assert.Equal("Work order cannot start while waiting for customer approval.", problem.Detail);
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
            Type: 0,
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
        int Type,
        decimal Cost,
        decimal Price,
        int StockQuantity);

    private sealed record InventoryItemResponse(
        Guid Id,
        string Name,
        string Description,
        int Type,
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
