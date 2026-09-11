using System.Net;
using System.Net.Http.Json;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Tests.Integration.Api.Customers.Contracts;
using GarageFlow.Tests.Integration.Api.Users.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Seed;
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.Users;

namespace GarageFlow.Tests.Integration.Api.Customers;

public sealed class CustomerStatusApiTests(GarageFlowApiFixture fixture)
    : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task ChangeStatus_ShouldReturn401_WhenRequestIsAnonymous()
    {
        using var client = _fixture.CreateClient();

        using var response = await client.PatchAsJsonAsync(
            $"/customers/{Guid.NewGuid()}/status",
            new ChangeCustomerStatusRequest("Suspended"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeStatus_ShouldReturn200AndPersistStatus_WhenActiveAdminRequestsTransition()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var customerId = await CustomerSeed.CreateIdAsync(client, CustomerSeed.CreateUniqueBuilder());

        using var response = await client.PatchAsJsonAsync(
            $"/customers/{customerId}/status",
            new ChangeCustomerStatusRequest("Suspended"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ChangeCustomerStatusResponse>(response);
        Assert.Equal(customerId, payload.Id);
        Assert.Equal("Suspended", payload.Status);

        using var getResponse = await client.GetAsync($"/customers/{customerId}");
        HttpResponseAssertions.AssertStatus(getResponse, HttpStatusCode.OK);
        var customer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(getResponse);
        Assert.Equal("Suspended", customer.Status);

        using var listResponse = await client.GetAsync("/customers?page=1&pageSize=100");
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var customers = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerListResponse>(listResponse);
        Assert.Equal(
            "Suspended",
            Assert.Single(customers.Items, item => item.Id == customerId).Status);
    }

    [Fact]
    public async Task ChangeStatus_ShouldReturn403_WhenActiveCustomerRequestsTransition()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var customer = await CreatePortalCustomerAsync(client);
        await ActivateCustomerLoginAsync(client, customer);

        using var response = await client.PatchAsJsonAsync(
            $"/customers/{customer.CustomerId}/status",
            new ChangeCustomerStatusRequest("Suspended"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeStatus_ShouldReturn403_WhenActiveAttendantRequestsTransition()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var targetCustomerId = await CustomerSeed.CreateIdAsync(client, CustomerSeed.CreateUniqueBuilder());
        var attendant = await CreateUserAsync(client, UserRole.Attendant);
        await ActivateUserAsync(client, attendant, "Attendant.Active#456");

        using var response = await client.PatchAsJsonAsync(
            $"/customers/{targetCustomerId}/status",
            new ChangeCustomerStatusRequest("Suspended"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeStatus_ShouldReturn403_WhenAdminMustChangePassword()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var targetCustomerId = await CustomerSeed.CreateIdAsync(client, CustomerSeed.CreateUniqueBuilder());
        var temporaryAdmin = await CreateUserAsync(client, UserRole.Admin);
        var initialPassword = User.GenerateInitialPassword(
            FullName.Create(temporaryAdmin.FullName),
            temporaryAdmin.BirthDate);
        await client.LoginAndAttachBearerTokenAsync(temporaryAdmin.Email, initialPassword);

        using var response = await client.PatchAsJsonAsync(
            $"/customers/{targetCustomerId}/status",
            new ChangeCustomerStatusRequest("Suspended"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeStatus_ShouldReturn400_WhenStatusIsNotAnExactSupportedName()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var customerId = await CustomerSeed.CreateIdAsync(client, CustomerSeed.CreateUniqueBuilder());

        using var response = await client.PatchAsJsonAsync(
            $"/customers/{customerId}/status",
            new ChangeCustomerStatusRequest("1"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStatus_ShouldReturn404_WhenCustomerDoesNotExist()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        using var response = await client.PatchAsJsonAsync(
            $"/customers/{Guid.NewGuid()}/status",
            new ChangeCustomerStatusRequest("Suspended"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
    }

    private static async Task<PortalCustomer> CreatePortalCustomerAsync(HttpClient client)
    {
        var seed = Guid.NewGuid().ToString("N");
        var request = CustomerSeed.CreateUniqueBuilder()
            .WithFullName($"Status Customer {seed[..8]}")
            .WithEmail($"status.customer.{seed}@example.com")
            .WithPhoneNumber("11987654321")
            .BuildCreateRequest();
        using var createResponse = await client.PostAsJsonAsync("/customers", request);
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var customer = await HttpResponseAssertions.ReadRequiredJsonAsync<CustomerResponse>(createResponse);

        var birthDate = new DateOnly(1991, 5, 17);
        using var activateResponse = await client.PostAsJsonAsync(
            $"/customers/{customer.Id}/portal-user",
            new ActivateCustomerPortalUserRequest(birthDate));
        HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
        var user = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

        return new PortalCustomer(customer.Id, user.FullName, user.Email, birthDate);
    }

    private static async Task ActivateCustomerLoginAsync(HttpClient client, PortalCustomer customer)
    {
        var initialPassword = User.GenerateInitialPassword(
            FullName.Create(customer.FullName),
            customer.BirthDate);
        await client.LoginAndAttachBearerTokenAsync(customer.Email, initialPassword);

        using var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(initialPassword, "Customer.Active#456"));
        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);
        await client.LoginAndAttachBearerTokenAsync(customer.Email, "Customer.Active#456");
    }

    private static async Task<CreateUserResponse> CreateUserAsync(HttpClient client, UserRole role)
    {
        var seed = Guid.NewGuid().ToString("N");
        var request = new UserBuilder()
            .WithFullName($"Status {role} {seed[..8]}")
            .WithEmail($"status.{role.ToString().ToLowerInvariant()}.{seed}@example.com")
            .WithBirthDate(new DateOnly(1990, 4, 12))
            .WithRole(role)
            .BuildCreateRequest();
        using var response = await client.PostAsJsonAsync("/users", request);
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
        return await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(response);
    }

    private static async Task ActivateUserAsync(
        HttpClient client,
        CreateUserResponse user,
        string newPassword)
    {
        var initialPassword = User.GenerateInitialPassword(FullName.Create(user.FullName), user.BirthDate);
        await client.LoginAndAttachBearerTokenAsync(user.Email, initialPassword);
        using var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(initialPassword, newPassword));
        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);
        await client.LoginAndAttachBearerTokenAsync(user.Email, newPassword);
    }

    private sealed record ChangeCustomerStatusRequest(string Status);

    private sealed record ChangeCustomerStatusResponse(Guid Id, string Status);

    private sealed record CustomerListResponse(IReadOnlyList<CustomerResponse> Items);

    private sealed record PortalCustomer(
        Guid CustomerId,
        string FullName,
        string Email,
        DateOnly BirthDate);
}
