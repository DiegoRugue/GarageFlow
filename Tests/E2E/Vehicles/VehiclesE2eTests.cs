using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Contracts.Common;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.Vehicles;

public sealed class VehiclesE2eTests(E2eApiFixture fixture) : IClassFixture<E2eApiFixture>
{
    private const string ConflictProblemTitle = "Business rule violation";

    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task VehicleLookups_ShouldSupportBrandModelColorContracts()
    {
        // Given an authenticated admin and unique lookup names for brand, model, and color.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");

        // When the admin creates, lists, fetches, and updates a vehicle brand.
        var brandName = $"E2E Brand {uniqueSeed[..8]}";
        var updatedBrandName = $"E2E Brand Updated {uniqueSeed[..8]}";
        var createdBrand = await CreateVehicleBrandAsync(client, brandName);
        var listedBrand = await GetBrandFromListAsync(client, createdBrand.Id);
        Assert.Equal(createdBrand.Id, listedBrand.Id);
        Assert.Equal(brandName, listedBrand.Name);

        var getBrandPayload = await GetVehicleBrandAsync(client, createdBrand.Id);
        Assert.Equal(createdBrand.Id, getBrandPayload.Id);
        Assert.Equal(brandName, getBrandPayload.Name);
        Assert.NotEqual(default, getBrandPayload.CreatedAt);

        using var updateBrandResponse = await client.PutAsJsonAsync(
            $"/vehicle-brands/{createdBrand.Id}",
            new UpdateVehicleBrandRequest(updatedBrandName));

        HttpResponseAssertions.AssertStatus(updateBrandResponse, HttpStatusCode.OK);

        var updatedBrand = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleBrandResponse>(updateBrandResponse);
        Assert.Equal(createdBrand.Id, updatedBrand.Id);
        Assert.Equal(updatedBrandName, updatedBrand.Name);
        Assert.NotEqual(default, updatedBrand.CreatedAt);

        // And the admin creates, lists, fetches, and updates a model under that brand.
        var modelName = $"E2E Model {uniqueSeed[..8]}";
        var updatedModelName = $"E2E Model Updated {uniqueSeed[..8]}";
        var createdModel = await CreateVehicleModelAsync(client, createdBrand.Id, modelName);
        var listedModel = await GetModelFromListAsync(client, createdModel.Id, createdBrand.Id);
        Assert.Equal(createdModel.Id, listedModel.Id);
        Assert.Equal(createdBrand.Id, listedModel.VehicleBrandId);
        Assert.Equal(modelName, listedModel.Name);

        var getModelPayload = await GetVehicleModelAsync(client, createdModel.Id);
        Assert.Equal(createdModel.Id, getModelPayload.Id);
        Assert.Equal(createdBrand.Id, getModelPayload.VehicleBrandId);
        Assert.Equal(modelName, getModelPayload.Name);
        Assert.NotEqual(default, getModelPayload.CreatedAt);

        using var updateModelResponse = await client.PutAsJsonAsync(
            $"/vehicle-models/{createdModel.Id}",
            new UpdateVehicleModelRequest(createdBrand.Id, updatedModelName));

        HttpResponseAssertions.AssertStatus(updateModelResponse, HttpStatusCode.OK);

        var updatedModel = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleModelResponse>(updateModelResponse);
        Assert.Equal(createdModel.Id, updatedModel.Id);
        Assert.Equal(createdBrand.Id, updatedModel.VehicleBrandId);
        Assert.Equal(updatedModelName, updatedModel.Name);
        Assert.NotEqual(default, updatedModel.CreatedAt);

        // And the admin creates, lists, fetches, and updates a vehicle color.
        var colorName = $"E2E Color {uniqueSeed[..8]}";
        var updatedColorName = $"E2E Color Updated {uniqueSeed[..8]}";
        var createdColor = await CreateVehicleColorAsync(client, colorName);
        var listedColor = await GetColorFromListAsync(client, createdColor.Id);
        Assert.Equal(createdColor.Id, listedColor.Id);
        Assert.Equal(colorName, listedColor.Name);

        var getColorPayload = await GetVehicleColorAsync(client, createdColor.Id);
        Assert.Equal(createdColor.Id, getColorPayload.Id);
        Assert.Equal(colorName, getColorPayload.Name);
        Assert.NotEqual(default, getColorPayload.CreatedAt);

        using var updateColorResponse = await client.PutAsJsonAsync(
            $"/vehicle-colors/{createdColor.Id}",
            new UpdateVehicleColorRequest(updatedColorName));

        HttpResponseAssertions.AssertStatus(updateColorResponse, HttpStatusCode.OK);

        var updatedColor = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleColorResponse>(updateColorResponse);
        Assert.Equal(createdColor.Id, updatedColor.Id);
        Assert.Equal(updatedColorName, updatedColor.Name);
        Assert.NotEqual(default, updatedColor.CreatedAt);

        // Then unused lookup values can be deleted and become unavailable by id.
        using var deleteModelResponse = await client.DeleteAsync($"/vehicle-models/{createdModel.Id}");
        HttpResponseAssertions.AssertStatus(deleteModelResponse, HttpStatusCode.NoContent);

        using var getDeletedModelResponse = await client.GetAsync($"/vehicle-models/{createdModel.Id}");
        HttpResponseAssertions.AssertStatus(getDeletedModelResponse, HttpStatusCode.NotFound);
        var deletedModelProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(getDeletedModelResponse);
        Assert.Equal((int)HttpStatusCode.NotFound, deletedModelProblem.Status);

        using var deleteBrandResponse = await client.DeleteAsync($"/vehicle-brands/{createdBrand.Id}");
        HttpResponseAssertions.AssertStatus(deleteBrandResponse, HttpStatusCode.NoContent);

        using var getDeletedBrandResponse = await client.GetAsync($"/vehicle-brands/{createdBrand.Id}");
        HttpResponseAssertions.AssertStatus(getDeletedBrandResponse, HttpStatusCode.NotFound);
        var deletedBrandProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(getDeletedBrandResponse);
        Assert.Equal((int)HttpStatusCode.NotFound, deletedBrandProblem.Status);

        using var deleteColorResponse = await client.DeleteAsync($"/vehicle-colors/{createdColor.Id}");
        HttpResponseAssertions.AssertStatus(deleteColorResponse, HttpStatusCode.NoContent);

        using var getDeletedColorResponse = await client.GetAsync($"/vehicle-colors/{createdColor.Id}");
        HttpResponseAssertions.AssertStatus(getDeletedColorResponse, HttpStatusCode.NotFound);
        var deletedColorProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(getDeletedColorResponse);
        Assert.Equal((int)HttpStatusCode.NotFound, deletedColorProblem.Status);
    }

    [Fact]
    public async Task VehicleLookups_ShouldReturnConflict_ForDuplicateNamesWithDifferentCase()
    {
        // Given an authenticated admin and existing lookup names saved in uppercase.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N")[..8];

        // When the admin creates the same brand name with different casing.
        var upperBrandName = $"E2EBRAND{uniqueSeed}";
        var lowerBrandName = upperBrandName.ToLowerInvariant();
        var createdBrand = await CreateVehicleBrandAsync(client, upperBrandName);

        using var duplicateBrandResponse = await client.PostAsJsonAsync(
            "/vehicle-brands",
            new CreateVehicleBrandRequest(lowerBrandName));

        // Then the brand uniqueness rule is enforced case-insensitively.
        HttpResponseAssertions.AssertStatus(duplicateBrandResponse, HttpStatusCode.Conflict);

        var duplicateBrandProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(duplicateBrandResponse);
        Assert.Equal((int)HttpStatusCode.Conflict, duplicateBrandProblem.Status);
        Assert.Equal(ConflictProblemTitle, duplicateBrandProblem.Title);

        var upperModelName = $"E2EMODEL{uniqueSeed}";
        var lowerModelName = upperModelName.ToLowerInvariant();
        _ = await CreateVehicleModelAsync(client, createdBrand.Id, upperModelName);

        // When the admin creates the same model name with different casing under the same brand.
        using var duplicateModelResponse = await client.PostAsJsonAsync(
            "/vehicle-models",
            new CreateVehicleModelRequest(createdBrand.Id, lowerModelName));

        // Then the model uniqueness rule is enforced case-insensitively per brand.
        HttpResponseAssertions.AssertStatus(duplicateModelResponse, HttpStatusCode.Conflict);

        var duplicateModelProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(duplicateModelResponse);
        Assert.Equal((int)HttpStatusCode.Conflict, duplicateModelProblem.Status);
        Assert.Equal(ConflictProblemTitle, duplicateModelProblem.Title);

        var upperColorName = $"E2ECOLOR{uniqueSeed}";
        var lowerColorName = upperColorName.ToLowerInvariant();
        _ = await CreateVehicleColorAsync(client, upperColorName);

        // When the admin creates the same color name with different casing.
        using var duplicateColorResponse = await client.PostAsJsonAsync(
            "/vehicle-colors",
            new CreateVehicleColorRequest(lowerColorName));

        // Then the color uniqueness rule is enforced case-insensitively.
        HttpResponseAssertions.AssertStatus(duplicateColorResponse, HttpStatusCode.Conflict);

        var duplicateColorProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(duplicateColorResponse);
        Assert.Equal((int)HttpStatusCode.Conflict, duplicateColorProblem.Status);
        Assert.Equal(ConflictProblemTitle, duplicateColorProblem.Title);
    }

    [Fact]
    public async Task Vehicles_ShouldSupportCreateListGetUpdateAndDeleteContracts()
    {
        // Given an authenticated admin and all dependencies required to register a vehicle.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");

        var createdCustomer = await CreateCustomerAsync(client, uniqueSeed);
        var createdBrand = await CreateVehicleBrandAsync(client, $"E2E Brand {uniqueSeed[..8]}");
        var createdModel = await CreateVehicleModelAsync(client, createdBrand.Id, $"E2E Model {uniqueSeed[..8]}");
        var createdColor = await CreateVehicleColorAsync(client, $"E2E Color {uniqueSeed[..8]}");

        // When the admin creates the vehicle.
        var plate = GenerateMercosulPlate(uniqueSeed);
        using var createVehicleResponse = await client.PostAsJsonAsync(
            "/vehicles",
            new CreateVehicleRequest(
                Plate: plate,
                Year: 2023,
                CustomerId: createdCustomer.Id,
                VehicleModelId: createdModel.Id,
                VehicleColorId: createdColor.Id));

        // Then the created vehicle links back to customer, brand, model, and color.
        HttpResponseAssertions.AssertStatus(createVehicleResponse, HttpStatusCode.Created);

        var createdVehicle = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleResponse>(createVehicleResponse);
        Assert.NotEqual(Guid.Empty, createdVehicle.Id);
        Assert.Equal(createdCustomer.Id, createdVehicle.CustomerId);
        Assert.Equal(2023, createdVehicle.Year);
        Assert.Equal(createdBrand.Id, createdVehicle.VehicleBrandId);
        Assert.Equal(createdModel.Id, createdVehicle.VehicleModelId);
        Assert.Equal(createdColor.Id, createdVehicle.VehicleColorId);
        Assert.Equal(plate, createdVehicle.Plate);
        Assert.NotEqual(default, createdVehicle.CreatedAt);

        // When the admin lists vehicles.
        using var listVehiclesResponse = await client.GetAsync("/vehicles?page=1&pageSize=20");

        // Then the vehicle appears with lookup display names included.
        HttpResponseAssertions.AssertStatus(listVehiclesResponse, HttpStatusCode.OK);

        var listVehicles = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleResponse>>(listVehiclesResponse);
        Assert.Equal(1, listVehicles.Page);
        Assert.Equal(20, listVehicles.PageSize);
        Assert.True(listVehicles.TotalCount >= 1);

        var listedVehicle = listVehicles.Items.FirstOrDefault(item => item.Id == createdVehicle.Id);
        Assert.NotNull(listedVehicle);
        Assert.Equal(createdVehicle.CustomerId, listedVehicle!.CustomerId);
        Assert.Equal(createdVehicle.Year, listedVehicle.Year);
        Assert.Equal(createdBrand.Id, listedVehicle.VehicleBrandId);
        Assert.Equal(createdModel.Id, listedVehicle.VehicleModelId);
        Assert.Equal(createdColor.Id, listedVehicle.VehicleColorId);
        Assert.Equal(plate, listedVehicle.Plate);
        Assert.Equal(createdBrand.Name, listedVehicle.VehicleBrandName);
        Assert.Equal(createdModel.Name, listedVehicle.VehicleModelName);
        Assert.Equal(createdColor.Name, listedVehicle.VehicleColorName);
        Assert.NotEqual(default, listedVehicle.CreatedAt);

        // When the admin fetches the vehicle by id.
        using var getVehicleResponse = await client.GetAsync($"/vehicles/{createdVehicle.Id}");

        // Then the details contract includes persisted ids and lookup names.
        HttpResponseAssertions.AssertStatus(getVehicleResponse, HttpStatusCode.OK);

        var vehiclePayload = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleResponse>(getVehicleResponse);
        Assert.Equal(createdVehicle.Id, vehiclePayload.Id);
        Assert.Equal(createdVehicle.CustomerId, vehiclePayload.CustomerId);
        Assert.Equal(createdVehicle.Year, vehiclePayload.Year);
        Assert.Equal(createdBrand.Id, vehiclePayload.VehicleBrandId);
        Assert.Equal(createdModel.Id, vehiclePayload.VehicleModelId);
        Assert.Equal(createdColor.Id, vehiclePayload.VehicleColorId);
        Assert.Equal(plate, vehiclePayload.Plate);
        Assert.Equal(createdBrand.Name, vehiclePayload.VehicleBrandName);
        Assert.Equal(createdModel.Name, vehiclePayload.VehicleModelName);
        Assert.Equal(createdColor.Name, vehiclePayload.VehicleColorName);
        Assert.NotEqual(default, vehiclePayload.CreatedAt);

        // When the admin updates mutable vehicle fields.
        var updatedPlate = GenerateMercosulPlate($"{uniqueSeed[..24]}ff");
        using var updateVehicleResponse = await client.PutAsJsonAsync(
            $"/vehicles/{createdVehicle.Id}",
            new UpdateVehicleRequest(
                Plate: updatedPlate,
                Year: 2025,
                CustomerId: createdCustomer.Id,
                VehicleModelId: createdModel.Id,
                VehicleColorId: createdColor.Id));

        // Then the update response reflects the new year and plate.
        HttpResponseAssertions.AssertStatus(updateVehicleResponse, HttpStatusCode.OK);

        var updatedVehicle = await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleResponse>(updateVehicleResponse);
        Assert.Equal(createdVehicle.Id, updatedVehicle.Id);
        Assert.Equal(createdCustomer.Id, updatedVehicle.CustomerId);
        Assert.Equal(2025, updatedVehicle.Year);
        Assert.Equal(createdBrand.Id, updatedVehicle.VehicleBrandId);
        Assert.Equal(createdModel.Id, updatedVehicle.VehicleModelId);
        Assert.Equal(createdColor.Id, updatedVehicle.VehicleColorId);
        Assert.Equal(updatedPlate, updatedVehicle.Plate);
        Assert.NotEqual(default, updatedVehicle.CreatedAt);

        // When the admin deletes the vehicle.
        using var deleteVehicleResponse = await client.DeleteAsync($"/vehicles/{createdVehicle.Id}");
        HttpResponseAssertions.AssertStatus(deleteVehicleResponse, HttpStatusCode.NoContent);

        // Then the deleted vehicle is no longer available by id.
        using var getDeletedVehicleResponse = await client.GetAsync($"/vehicles/{createdVehicle.Id}");
        HttpResponseAssertions.AssertStatus(getDeletedVehicleResponse, HttpStatusCode.NotFound);

        var notFoundVehicleProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(getDeletedVehicleResponse);
        Assert.Equal((int)HttpStatusCode.NotFound, notFoundVehicleProblem.Status);
    }

    [Fact]
    public async Task VehicleLookups_ShouldReturnConflict_WhenDeletingValuesUsedByVehicle()
    {
        // Given an authenticated admin and a vehicle that references brand, model, and color lookups.
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueSeed = Guid.NewGuid().ToString("N");

        var createdCustomer = await CreateCustomerAsync(client, uniqueSeed);
        var createdBrand = await CreateVehicleBrandAsync(client, $"E2E Brand {uniqueSeed[..8]}");
        var createdModel = await CreateVehicleModelAsync(client, createdBrand.Id, $"E2E Model {uniqueSeed[..8]}");
        var createdColor = await CreateVehicleColorAsync(client, $"E2E Color {uniqueSeed[..8]}");

        using var createVehicleResponse = await client.PostAsJsonAsync(
            "/vehicles",
            new CreateVehicleRequest(
                Plate: GenerateMercosulPlate(uniqueSeed),
                Year: 2024,
                CustomerId: createdCustomer.Id,
                VehicleModelId: createdModel.Id,
                VehicleColorId: createdColor.Id));

        HttpResponseAssertions.AssertStatus(createVehicleResponse, HttpStatusCode.Created);
        _ = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleResponse>(createVehicleResponse);

        // When the admin tries to delete a referenced brand.
        using var deleteBrandResponse = await client.DeleteAsync($"/vehicle-brands/{createdBrand.Id}");

        // Then the API blocks the delete to preserve vehicle consistency.
        HttpResponseAssertions.AssertStatus(deleteBrandResponse, HttpStatusCode.Conflict);
        var deleteBrandProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(deleteBrandResponse);
        Assert.Equal((int)HttpStatusCode.Conflict, deleteBrandProblem.Status);
        Assert.Equal(ConflictProblemTitle, deleteBrandProblem.Title);

        // When the admin tries to delete a referenced model.
        using var deleteModelResponse = await client.DeleteAsync($"/vehicle-models/{createdModel.Id}");

        // Then the API returns the same business-rule conflict contract.
        HttpResponseAssertions.AssertStatus(deleteModelResponse, HttpStatusCode.Conflict);
        var deleteModelProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(deleteModelResponse);
        Assert.Equal((int)HttpStatusCode.Conflict, deleteModelProblem.Status);
        Assert.Equal(ConflictProblemTitle, deleteModelProblem.Title);

        // When the admin tries to delete a referenced color.
        using var deleteColorResponse = await client.DeleteAsync($"/vehicle-colors/{createdColor.Id}");

        // Then the API returns the same business-rule conflict contract.
        HttpResponseAssertions.AssertStatus(deleteColorResponse, HttpStatusCode.Conflict);
        var deleteColorProblem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(deleteColorResponse);
        Assert.Equal((int)HttpStatusCode.Conflict, deleteColorProblem.Status);
        Assert.Equal(ConflictProblemTitle, deleteColorProblem.Title);
    }

    private static async Task<CreateCustomerResponse> CreateCustomerAsync(HttpClient client, string seed)
    {
        var request = new CreateCustomerRequest(
            TaxDocument: GenerateValidCpf(seed),
            FullName: $"E2E Customer {seed[..8]}",
            Email: $"e2e-vehicle-customer-{seed}@garageflow.local",
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

    private static async Task<CreateVehicleBrandResponse> CreateVehicleBrandAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/vehicle-brands", new CreateVehicleBrandRequest(name));
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleBrandResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(name, payload.Name);
        Assert.NotEqual(default, payload.CreatedAt);

        return payload;
    }

    private static async Task<CreateVehicleModelResponse> CreateVehicleModelAsync(HttpClient client, Guid brandId, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/vehicle-models",
            new CreateVehicleModelRequest(brandId, name));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleModelResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(brandId, payload.VehicleBrandId);
        Assert.Equal(name, payload.Name);
        Assert.NotEqual(default, payload.CreatedAt);

        return payload;
    }

    private static async Task<CreateVehicleColorResponse> CreateVehicleColorAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/vehicle-colors", new CreateVehicleColorRequest(name));
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateVehicleColorResponse>(response);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(name, payload.Name);
        Assert.NotEqual(default, payload.CreatedAt);

        return payload;
    }

    private static async Task<VehicleBrandResponse> GetBrandFromListAsync(HttpClient client, Guid brandId)
    {
        using var response = await client.GetAsync("/vehicle-brands?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleBrandResponse>>(response);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.True(payload.TotalCount >= 1);

        var item = payload.Items.FirstOrDefault(entry => entry.Id == brandId);
        Assert.NotNull(item);
        return item!;
    }

    private static async Task<VehicleModelResponse> GetModelFromListAsync(HttpClient client, Guid modelId, Guid brandId)
    {
        using var response = await client.GetAsync($"/vehicle-models?page=1&pageSize=20&vehicleBrandId={brandId}");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleModelResponse>>(response);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.True(payload.TotalCount >= 1);

        var item = payload.Items.FirstOrDefault(entry => entry.Id == modelId);
        Assert.NotNull(item);
        return item!;
    }

    private static async Task<VehicleColorResponse> GetColorFromListAsync(HttpClient client, Guid colorId)
    {
        using var response = await client.GetAsync("/vehicle-colors?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<PaginatedResponse<VehicleColorResponse>>(response);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.True(payload.TotalCount >= 1);

        var item = payload.Items.FirstOrDefault(entry => entry.Id == colorId);
        Assert.NotNull(item);
        return item!;
    }

    private static async Task<VehicleBrandResponse> GetVehicleBrandAsync(HttpClient client, Guid brandId)
    {
        using var response = await client.GetAsync($"/vehicle-brands/{brandId}");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleBrandResponse>(response);
    }

    private static async Task<VehicleModelResponse> GetVehicleModelAsync(HttpClient client, Guid modelId)
    {
        using var response = await client.GetAsync($"/vehicle-models/{modelId}");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleModelResponse>(response);
    }

    private static async Task<VehicleColorResponse> GetVehicleColorAsync(HttpClient client, Guid colorId)
    {
        using var response = await client.GetAsync($"/vehicle-colors/{colorId}");
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<VehicleColorResponse>(response);
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

    private sealed record CreateVehicleBrandRequest(string Name);

    private sealed record CreateVehicleBrandResponse(
        Guid Id,
        string Name,
        DateTime CreatedAt);

    private sealed record UpdateVehicleBrandRequest(string Name);

    private sealed record VehicleBrandResponse(
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

    private sealed record UpdateVehicleModelRequest(
        Guid VehicleBrandId,
        string Name);

    private sealed record VehicleModelResponse(
        Guid Id,
        Guid VehicleBrandId,
        string Name,
        DateTime CreatedAt);

    private sealed record CreateVehicleColorRequest(string Name);

    private sealed record CreateVehicleColorResponse(
        Guid Id,
        string Name,
        DateTime CreatedAt);

    private sealed record UpdateVehicleColorRequest(string Name);

    private sealed record VehicleColorResponse(
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

    private sealed record UpdateVehicleRequest(
        string Plate,
        int Year,
        Guid CustomerId,
        Guid VehicleModelId,
        Guid VehicleColorId);

    private sealed record VehicleResponse(
        Guid Id,
        Guid CustomerId,
        int Year,
        Guid VehicleBrandId,
        Guid VehicleModelId,
        Guid VehicleColorId,
        string Plate,
        DateTime CreatedAt,
        string? VehicleBrandName = null,
        string? VehicleModelName = null,
        string? VehicleColorName = null);
}
