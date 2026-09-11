using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.Customers;

[Collection(E2eApiCollection.Name)]
public sealed class CustomerStatusE2eTests(E2eApiFixture fixture)
{
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task SuspendedCustomer_ShouldImmediatelyLoseWorkOrderAndLoginAccess_WithPreviouslyIssuedToken()
    {
        using var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        var customer = await CreatePortalCustomerAsync(adminClient);
        using var customerClient = _fixture.CreateClient();
        var initialLogin = await customerClient.LoginAndAttachBearerTokenAsync(
            customer.Email,
            customer.InitialPassword);
        Assert.True(initialLogin.MustChangePassword);

        const string activePassword = "Customer.Status#456";
        using var changePasswordResponse = await customerClient.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(customer.InitialPassword, activePassword));
        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);
        await customerClient.LoginAndAttachBearerTokenAsync(customer.Email, activePassword);

        using var activeAccessResponse = await customerClient.GetAsync("/me/work-orders?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(activeAccessResponse, HttpStatusCode.OK);

        using var suspendResponse = await adminClient.PatchAsJsonAsync(
            $"/customers/{customer.Id}/status",
            new ChangeCustomerStatusRequest("Suspended"));
        HttpResponseAssertions.AssertStatus(suspendResponse, HttpStatusCode.OK);
        var suspended = await HttpResponseAssertions.ReadRequiredJsonAsync<ChangeCustomerStatusResponse>(suspendResponse);
        Assert.Equal(customer.Id, suspended.Id);
        Assert.Equal("Suspended", suspended.Status);

        using var getCustomerResponse = await adminClient.GetAsync($"/customers/{customer.Id}");
        HttpResponseAssertions.AssertStatus(getCustomerResponse, HttpStatusCode.OK);
        var persisted = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(getCustomerResponse);
        Assert.Equal("Suspended", persisted.Status);

        using var deniedAccessResponse = await customerClient.GetAsync("/me/work-orders?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(deniedAccessResponse, HttpStatusCode.Unauthorized);

        using var deniedLoginResponse = await customerClient.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(customer.Email, activePassword));
        HttpResponseAssertions.AssertStatus(deniedLoginResponse, HttpStatusCode.Unauthorized);

        using var staffAccessResponse = await adminClient.GetAsync("/work-orders?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(staffAccessResponse, HttpStatusCode.OK);
    }

    private static async Task<PortalCustomer> CreatePortalCustomerAsync(HttpClient adminClient)
    {
        var seed = Guid.NewGuid().ToString("N");
        var fullName = $"E2E Status {seed[..8]} Runner";
        var email = $"e2e-status-{seed}@garageflow.local";
        var birthDate = new DateOnly(1991, 6, 15);
        var request = new CreateCustomerRequest(
            TaxDocument: GenerateValidCpf(seed),
            FullName: fullName,
            Email: email,
            PhoneNumber: GeneratePhoneNumber(seed));

        using var createResponse = await adminClient.PostAsJsonAsync("/customers", request);
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var customer = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateCustomerResponse>(createResponse);

        using var activationResponse = await adminClient.PostAsJsonAsync(
            $"/customers/{customer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(birthDate));
        HttpResponseAssertions.AssertStatus(activationResponse, HttpStatusCode.Created);

        return new PortalCustomer(
            customer.Id,
            fullName,
            email,
            InitialPassword: $"runner{birthDate.Year}");
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
        var digits = new int[9];
        for (var index = 0; index < digits.Length; index++)
        {
            digits[index] = seedDigits[index % seedDigits.Length];
        }

        if (digits.Distinct().Count() == 1)
        {
            digits[8] = (digits[8] + 1) % 10;
        }

        var first = CalculateCpfCheckDigit(digits, 10);
        var firstTen = new int[10];
        Array.Copy(digits, firstTen, digits.Length);
        firstTen[9] = first;
        var second = CalculateCpfCheckDigit(firstTen, 11);
        return string.Concat(digits.Select(static digit => (char)('0' + digit)))
            + (char)('0' + first)
            + (char)('0' + second);
    }

    private static int CalculateCpfCheckDigit(int[] digits, int startWeight)
    {
        var sum = digits.Select((digit, index) => digit * (startWeight - index)).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private sealed record PortalCustomer(Guid Id, string FullName, string Email, string InitialPassword);

    private sealed record CreateCustomerRequest(string TaxDocument, string FullName, string Email, string PhoneNumber);

    private sealed record CreateCustomerResponse(
        Guid Id,
        string TaxDocument,
        string TaxDocumentType,
        string FullName,
        string Email,
        string PhoneNumber,
        DateTime CreatedAt);

    private sealed record ActivateCustomerPortalUserRequest(DateOnly BirthDate);

    private sealed record ChangeCustomerStatusRequest(string Status);

    private sealed record ChangeCustomerStatusResponse(Guid Id, string Status);

    private sealed record CustomerResponse(Guid Id, string Status);

    private sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);

    private sealed record LoginRequest(string Email, string Password);
}
