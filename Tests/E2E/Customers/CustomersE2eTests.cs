using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Contracts.Common;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.Customers;

[Collection(E2eApiCollection.Name)]
public sealed class CustomersE2eTests(E2eApiFixture fixture)
{
    private const string CustomerRole = "Customer";

    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task Customers_ShouldSupportCreateListGetUpdateAndDeleteContracts()
    {
        // Given an authenticated admin and a unique customer payload.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");
        var createRequest = BuildCreateCustomerRequest(uniqueSeed);

        // When the admin creates the customer.
        using var createResponse = await client.PostAsJsonAsync("/customers", createRequest);

        // Then the created-customer response mirrors the submitted contract.
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);

        var createdCustomer = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateCustomerResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, createdCustomer.Id);
        Assert.Equal(createRequest.TaxDocument, createdCustomer.TaxDocument);
        Assert.Equal("Cpf", createdCustomer.TaxDocumentType);
        Assert.Equal(createRequest.FullName, createdCustomer.FullName);
        Assert.Equal(createRequest.Email, createdCustomer.Email);
        Assert.Equal(createRequest.PhoneNumber, createdCustomer.PhoneNumber);
        Assert.NotEqual(default, createdCustomer.CreatedAt);

        // When the admin lists customers.
        using var listResponse = await client.GetAsync("/customers?page=1&pageSize=20");

        // Then the created customer appears in the paginated list.
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);

        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<CustomerListItemResponse>>(listResponse);
        Assert.Equal(1, listPayload.Page);
        Assert.Equal(20, listPayload.PageSize);
        Assert.True(listPayload.TotalCount >= 1);

        var createdFromList = listPayload.Items.FirstOrDefault(item => item.Id == createdCustomer.Id);
        Assert.NotNull(createdFromList);
        Assert.Equal(createdCustomer.TaxDocument, createdFromList!.TaxDocument);
        Assert.Equal(createdCustomer.TaxDocumentType, createdFromList.TaxDocumentType);
        Assert.Equal(createdCustomer.FullName, createdFromList.FullName);
        Assert.Equal(createdCustomer.Email, createdFromList.Email);
        Assert.Equal(createdCustomer.PhoneNumber, createdFromList.PhoneNumber);
        Assert.NotEqual(default, createdFromList.CreatedAt);

        // When the admin fetches the customer by id.
        using var getByIdResponse = await client.GetAsync($"/customers/{createdCustomer.Id}");

        // Then the details endpoint returns the same persisted customer fields.
        HttpResponseAssertions.AssertStatus(getByIdResponse, HttpStatusCode.OK);

        var getByIdPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(getByIdResponse);
        Assert.Equal(createdCustomer.Id, getByIdPayload.Id);
        Assert.Equal(createdCustomer.TaxDocument, getByIdPayload.TaxDocument);
        Assert.Equal(createdCustomer.TaxDocumentType, getByIdPayload.TaxDocumentType);
        Assert.Equal(createdCustomer.FullName, getByIdPayload.FullName);
        Assert.Equal(createdCustomer.Email, getByIdPayload.Email);
        Assert.Equal(createdCustomer.PhoneNumber, getByIdPayload.PhoneNumber);
        Assert.NotEqual(default, getByIdPayload.CreatedAt);

        var updateRequest = new UpdateCustomerRequest(
            FullName: $"Updated Customer {uniqueSeed[..8]}",
            Email: $"updated-customer-{uniqueSeed}@garageflow.local",
            PhoneNumber: "11912345678");

        // When the admin updates the customer.
        using var updateResponse = await client.PutAsJsonAsync($"/customers/{createdCustomer.Id}", updateRequest);

        // Then mutable fields are changed and immutable identity fields are preserved.
        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);

        var updatePayload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(updateResponse);
        Assert.Equal(createdCustomer.Id, updatePayload.Id);
        Assert.Equal(createdCustomer.TaxDocument, updatePayload.TaxDocument);
        Assert.Equal(createdCustomer.TaxDocumentType, updatePayload.TaxDocumentType);
        Assert.Equal(updateRequest.FullName, updatePayload.FullName);
        Assert.Equal(updateRequest.Email, updatePayload.Email);
        Assert.Equal(updateRequest.PhoneNumber, updatePayload.PhoneNumber);
        Assert.NotEqual(default, updatePayload.CreatedAt);

        // When the admin deletes the customer.
        using var deleteResponse = await client.DeleteAsync($"/customers/{createdCustomer.Id}");

        // Then the delete succeeds and the customer is no longer found.
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        using var getDeletedResponse = await client.GetAsync($"/customers/{createdCustomer.Id}");

        HttpResponseAssertions.AssertStatus(getDeletedResponse, HttpStatusCode.NotFound);

        var notFoundPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(getDeletedResponse);
        Assert.Equal((int)HttpStatusCode.NotFound, notFoundPayload.Status);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturnEmptyVehicles_WhenCustomerHasNoVehicles()
    {
        // Given an authenticated admin and a customer with no vehicle registrations.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var createdCustomer = await CreateCustomerAsync(client);

        // When the admin fetches that customer by id.
        using var response = await client.GetAsync($"/customers/{createdCustomer.Id}");

        // Then the details contract includes an empty vehicles collection.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(response);
        Assert.Equal(createdCustomer.Id, payload.Id);
        Assert.NotNull(payload.Vehicles);
        Assert.Empty(payload.Vehicles!);
    }

    [Fact]
    public async Task ActivateCustomerPortalUser_ShouldCreateCustomerUserContract()
    {
        // Given an authenticated admin and an existing customer.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var createdCustomer = await CreateCustomerAsync(client);

        var request = new ActivateCustomerPortalUserRequest(new DateOnly(1991, 1, 10));

        // When the admin activates portal access for that customer.
        using var response = await client.PostAsJsonAsync($"/customers/{createdCustomer.Id}/portal-user", request);

        // Then the API creates a customer user that must change the initial password.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(createdCustomer.Id, payload.CustomerId);
        Assert.Equal(createdCustomer.FullName, payload.FullName);
        Assert.Equal(createdCustomer.Email, payload.Email);
        Assert.Equal(request.BirthDate, payload.BirthDate);
        Assert.Equal(CustomerRole, payload.Role);
        Assert.True(payload.MustChangePassword);
        Assert.NotEqual(default, payload.CreatedAt);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturnVehicles_WhenCustomerHasVehicles()
    {
        // Given an authenticated admin, a customer, and vehicle lookup data created through the API.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");
        var createdCustomer = await CreateCustomerAsync(client, uniqueSeed);

        var brandName = $"E2E Brand {uniqueSeed[..8]}";
        using var createBrandResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new CreateVehicleBrandRequest(brandName));

        HttpResponseAssertions.AssertStatus(createBrandResponse, HttpStatusCode.Created);

        var createdBrand = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleBrandResponse>(createBrandResponse);
        Assert.NotEqual(Guid.Empty, createdBrand.Id);
        Assert.Equal(brandName, createdBrand.Name);
        Assert.NotEqual(default, createdBrand.CreatedAt);

        var modelName = $"E2E Model {uniqueSeed[..8]}";
        using var createModelResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new CreateVehicleModelRequest(createdBrand.Id, modelName));

        HttpResponseAssertions.AssertStatus(createModelResponse, HttpStatusCode.Created);

        var createdModel = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleModelResponse>(createModelResponse);
        Assert.NotEqual(Guid.Empty, createdModel.Id);
        Assert.Equal(createdBrand.Id, createdModel.VehicleBrandId);
        Assert.Equal(modelName, createdModel.Name);
        Assert.NotEqual(default, createdModel.CreatedAt);

        var colorName = $"E2E Color {uniqueSeed[..8]}";
        using var createColorResponse = await client.PostAsJsonAsync(
            "/vehicle-colors",
            new CreateVehicleColorRequest(colorName));

        HttpResponseAssertions.AssertStatus(createColorResponse, HttpStatusCode.Created);

        var createdColor = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleColorResponse>(createColorResponse);
        Assert.NotEqual(Guid.Empty, createdColor.Id);
        Assert.Equal(colorName, createdColor.Name);
        Assert.NotEqual(default, createdColor.CreatedAt);

        // When the admin registers a vehicle for the customer.
        var plate = GenerateMercosulPlate(uniqueSeed);
        using var createVehicleResponse = await client.PostAsJsonAsync(
            "/vehicles",
            new CreateVehicleRequest(
                Plate: plate,
                Year: 2024,
                CustomerId: createdCustomer.Id,
                VehicleModelId: createdModel.Id,
                VehicleColorId: createdColor.Id));

        HttpResponseAssertions.AssertStatus(createVehicleResponse, HttpStatusCode.Created);

        var createdVehicle = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleResponse>(createVehicleResponse);
        Assert.NotEqual(Guid.Empty, createdVehicle.Id);
        Assert.Equal(createdCustomer.Id, createdVehicle.CustomerId);
        Assert.Equal(2024, createdVehicle.Year);
        Assert.Equal(createdBrand.Id, createdVehicle.VehicleBrandId);
        Assert.Equal(createdModel.Id, createdVehicle.VehicleModelId);
        Assert.Equal(createdColor.Id, createdVehicle.VehicleColorId);
        Assert.Equal(plate, createdVehicle.Plate);
        Assert.NotEqual(default, createdVehicle.CreatedAt);

        // And the admin fetches the customer details.
        using var getCustomerResponse = await client.GetAsync($"/customers/{createdCustomer.Id}");

        // Then the customer details include the vehicle with joined brand, model, and color names.
        HttpResponseAssertions.AssertStatus(getCustomerResponse, HttpStatusCode.OK);

        var customerPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(getCustomerResponse);
        Assert.Equal(createdCustomer.Id, customerPayload.Id);
        Assert.NotNull(customerPayload.Vehicles);

        var vehicleFromCustomer = customerPayload.Vehicles!.FirstOrDefault(vehicle => vehicle.Id == createdVehicle.Id);
        Assert.NotNull(vehicleFromCustomer);
        Assert.Equal(createdVehicle.Id, vehicleFromCustomer!.Id);
        Assert.Equal(createdVehicle.Year, vehicleFromCustomer.Year);
        Assert.Equal(createdVehicle.Plate, vehicleFromCustomer.Plate);
        Assert.Equal(createdBrand.Id, vehicleFromCustomer.VehicleBrandId);
        Assert.Equal(brandName, vehicleFromCustomer.VehicleBrandName);
        Assert.Equal(createdModel.Id, vehicleFromCustomer.VehicleModelId);
        Assert.Equal(modelName, vehicleFromCustomer.VehicleModelName);
        Assert.Equal(createdColor.Id, vehicleFromCustomer.VehicleColorId);
        Assert.Equal(colorName, vehicleFromCustomer.VehicleColorName);
        Assert.NotEqual(default, vehicleFromCustomer.CreatedAt);
    }

    private static async Task<CreateCustomerResponse> CreateCustomerAsync(HttpClient client, string? uniqueSeed = null)
    {
        var request = BuildCreateCustomerRequest(uniqueSeed ?? Guid.NewGuid().ToString("N"));

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

    private static CreateCustomerRequest BuildCreateCustomerRequest(string uniqueSeed)
    {
        return new CreateCustomerRequest(
            TaxDocument: GenerateValidCpf(uniqueSeed),
            FullName: $"E2E Customer {uniqueSeed[..8]}",
            Email: $"e2e-customer-{uniqueSeed}@garageflow.local",
            PhoneNumber: GeneratePhoneNumber(uniqueSeed));
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
        if (string.IsNullOrWhiteSpace(seed))
        {
            throw new ArgumentException("Seed cannot be null or whitespace.", nameof(seed));
        }

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

    private sealed record CustomerListItemResponse(
        Guid Id,
        string TaxDocument,
        string TaxDocumentType,
        string FullName,
        string Email,
        string PhoneNumber,
        DateTime CreatedAt);

    private sealed record CustomerResponse(
        Guid Id,
        string TaxDocument,
        string TaxDocumentType,
        string FullName,
        string Email,
        string PhoneNumber,
        DateTime CreatedAt,
        IReadOnlyList<CustomerVehicleResponse>? Vehicles = null);

    private sealed record UpdateCustomerRequest(
        string FullName,
        string Email,
        string PhoneNumber);

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

    private sealed record CustomerVehicleResponse(
        Guid Id,
        int Year,
        string Plate,
        Guid VehicleBrandId,
        string VehicleBrandName,
        Guid VehicleModelId,
        string VehicleModelName,
        Guid VehicleColorId,
        string VehicleColorName,
        DateTime CreatedAt);

    private sealed record CreateVehicleBrandRequest(string Name);

    private sealed record CreateVehicleBrandResponse(
        Guid Id,
        string Name,
        DateTime CreatedAt);

    private sealed record CreateVehicleModelRequest(
        Guid VehicleBrandId,
        string Name);

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
}
