using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.Users;

[Collection(E2eApiCollection.Name)]
public sealed class UsersE2eTests(E2eApiFixture fixture)
{
    private const int AttendantRoleValue = 2;

    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task Users_ShouldSupportCreateListAndDeleteContracts()
    {
        // Given an authenticated admin and a unique attendant user payload.
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var email = $"e2e-user-{Guid.NewGuid():N}@garageflow.local";
        var fullName = "E2E Attendant User";
        var birthDate = new DateOnly(1995, 5, 5);

        // When the admin creates the user.
        using var createResponse = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest(
                FullName: fullName,
                Email: email,
                BirthDate: birthDate,
                Role: AttendantRoleValue));

        // Then the created-user response matches the request and default password policy.
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);

        var created = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(fullName, created.FullName);
        Assert.Equal(email, created.Email);
        Assert.Equal(birthDate, created.BirthDate);
        Assert.Equal(AttendantRoleValue, created.Role);
        Assert.True(created.MustChangePassword);
        Assert.NotEqual(default, created.CreatedAt);

        // When the admin lists users.
        using var listResponse = await client.GetAsync("/users?page=1&pageSize=20");

        // Then the created user appears in the paginated contract.
        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);

        var list = await HttpResponseAssertions.ReadRequiredJsonAsync<ListUsersResponse>(listResponse);
        Assert.Equal(1, list.Page);
        Assert.Equal(20, list.PageSize);
        Assert.True(list.TotalCount >= 2);

        var createdFromList = list.Items.FirstOrDefault(item => item.Id == created.Id);

        Assert.NotNull(createdFromList);
        Assert.Equal(fullName, createdFromList!.FullName);
        Assert.Equal(email, createdFromList!.Email);
        Assert.Equal(birthDate, createdFromList.BirthDate);
        Assert.Equal(AttendantRoleValue, createdFromList.Role);
        Assert.True(createdFromList.MustChangePassword);
        Assert.NotEqual(default, createdFromList.CreatedAt);
        Assert.NotEqual(default, createdFromList.UpdatedAt);

        // When the admin deletes the created user.
        using var deleteResponse = await client.DeleteAsync($"/users/{created.Id}");

        // Then the API confirms deletion with no response body.
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);
    }

    private sealed record CreateUserRequest(
        string FullName,
        string Email,
        DateOnly BirthDate,
        int Role);

    private sealed record CreateUserResponse(
        Guid Id,
        string FullName,
        string Email,
        DateOnly BirthDate,
        int Role,
        bool MustChangePassword,
        DateTime CreatedAt);

    private sealed record ListUsersResponse(
        IReadOnlyList<UserListItemResponse> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record UserListItemResponse(
        Guid Id,
        string FullName,
        string Email,
        DateOnly BirthDate,
        int Role,
        bool MustChangePassword,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
