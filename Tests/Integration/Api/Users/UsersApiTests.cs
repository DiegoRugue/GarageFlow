using System.Net;
using System.Net.Http.Json;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Tests.Integration.Api.Auth.Contracts;
using GarageFlow.Tests.Integration.Api.Users.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Shared.Users;

namespace GarageFlow.Tests.Integration.Api.Users;

public class UsersApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task Admin_ShouldCreateListAndDeleteUsers()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var createRequest = new UserBuilder()
            .WithFullName("Integration Attendant")
            .WithEmail($"integration.attendant.{uniqueToken}@example.com")
            .WithBirthDate(new DateOnly(1994, 4, 15))
            .WithRole(UserRole.Attendant)
            .BuildCreateRequest();

        var createResponse = await client.PostAsJsonAsync("/users", createRequest);

        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var createdUser = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createResponse);
        Assert.NotEqual(Guid.Empty, createdUser.Id);
        Assert.Equal(createRequest.Email, createdUser.Email);
        Assert.Equal(UserRole.Attendant, createdUser.Role);
        Assert.True(createdUser.MustChangePassword);

        var listResponse = await client.GetAsync("/users?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<ListUsersResponse>(listResponse);
        Assert.True(listPayload.TotalCount >= 2);
        Assert.Contains(listPayload.Items, item => item.Id == createdUser.Id && item.Email == createRequest.Email);

        var deleteResponse = await client.DeleteAsync($"/users/{createdUser.Id}");
        HttpResponseAssertions.AssertStatus(deleteResponse, HttpStatusCode.NoContent);

        var afterDeleteListResponse = await client.GetAsync("/users?page=1&pageSize=20");
        HttpResponseAssertions.AssertStatus(afterDeleteListResponse, HttpStatusCode.OK);
        var afterDeleteListPayload = await HttpResponseAssertions.ReadRequiredJsonAsync<ListUsersResponse>(afterDeleteListResponse);
        Assert.DoesNotContain(afterDeleteListPayload.Items, item => item.Id == createdUser.Id);
    }

    [Fact]
    public async Task Attendant_ShouldReceive403_WhenCreatingOrDeletingUsers()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");

        var attendantRequest = new UserBuilder()
            .WithFullName("Regular Attendant")
            .WithEmail($"regular.attendant.{uniqueToken}@example.com")
            .WithBirthDate(new DateOnly(1996, 11, 5))
            .WithRole(UserRole.Attendant)
            .BuildCreateRequest();

        var createAttendantResponse = await client.PostAsJsonAsync("/users", attendantRequest);
        HttpResponseAssertions.AssertStatus(createAttendantResponse, HttpStatusCode.Created);
        var createdAttendant = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createAttendantResponse);

        await AuthenticateCreatedUserAsActiveAsync(
            client,
            createdAttendant.Email,
            attendantRequest.FullName,
            attendantRequest.BirthDate,
            "Attendant.Active#123");

        var forbiddenCreateResponse = await client.PostAsJsonAsync(
            "/users",
            new UserBuilder()
                .WithFullName("Another User")
                .WithEmail($"another.user.{Guid.NewGuid():N}@example.com")
                .WithBirthDate(new DateOnly(1997, 2, 20))
                .WithRole(UserRole.Attendant)
                .BuildCreateRequest());

        var forbiddenListResponse = await client.GetAsync("/users?page=1&pageSize=20");

        HttpResponseAssertions.AssertStatus(forbiddenCreateResponse, HttpStatusCode.Forbidden);
        HttpResponseAssertions.AssertStatus(forbiddenListResponse, HttpStatusCode.Forbidden);

        var forbiddenDeleteResponse = await client.DeleteAsync($"/users/{Guid.NewGuid()}");
        HttpResponseAssertions.AssertStatus(forbiddenDeleteResponse, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_ShouldUpdateOwnProfileAndOwnPassword()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");

        var createRequest = new UserBuilder()
            .WithFullName("Profile User")
            .WithEmail($"profile.user.{uniqueToken}@example.com")
            .WithBirthDate(new DateOnly(1993, 8, 12))
            .WithRole(UserRole.Attendant)
            .BuildCreateRequest();

        var createResponse = await client.PostAsJsonAsync("/users", createRequest);
        HttpResponseAssertions.AssertStatus(createResponse, HttpStatusCode.Created);
        var createdUser = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createResponse);

        const string activePassword = "Profile.User.Active#123";
        await AuthenticateCreatedUserAsActiveAsync(
            client,
            createdUser.Email,
            createRequest.FullName,
            createRequest.BirthDate,
            activePassword);

        var updateRequest = new UserBuilder()
            .WithFullName("Profile User Updated")
            .WithEmail($"profile.user.updated.{uniqueToken}@example.com")
            .WithBirthDate(new DateOnly(1993, 8, 13))
            .BuildUpdateMyProfileRequest();

        var updateResponse = await client.PutAsJsonAsync("/users/me", updateRequest);

        HttpResponseAssertions.AssertStatus(updateResponse, HttpStatusCode.OK);
        var updatedProfile = await HttpResponseAssertions.ReadRequiredJsonAsync<UpdateMyProfileResponse>(updateResponse);
        Assert.Equal("Profile User Updated", updatedProfile.FullName);
        Assert.Equal($"profile.user.updated.{uniqueToken}@example.com", updatedProfile.Email);
        Assert.Equal(new DateOnly(1993, 8, 13), updatedProfile.BirthDate);
        Assert.Equal(UserRole.Attendant, updatedProfile.Role);

        const string newPassword = "Profile.User.New#123";
        var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: activePassword,
                NewPassword: newPassword));

        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);

        var login = await client.LoginAndAttachBearerTokenAsync(updatedProfile.Email, newPassword);
        Assert.False(login.MustChangePassword);
    }

    [Fact]
    public async Task FirstLoginUser_ShouldBeBlockedFromBusinessEndpoints_UntilPasswordIsChanged()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();
        var uniqueToken = Guid.NewGuid().ToString("N");
        var firstLoginUserRequest = new UserBuilder()
            .WithFullName("First Login User")
            .WithEmail($"first.login.{uniqueToken}@example.com")
            .WithBirthDate(new DateOnly(1995, 3, 21))
            .WithRole(UserRole.Attendant)
            .BuildCreateRequest();
        var createUserResponse = await client.PostAsJsonAsync("/users", firstLoginUserRequest);
        HttpResponseAssertions.AssertStatus(createUserResponse, HttpStatusCode.Created);
        var createdUser = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createUserResponse);

        var initialPassword = User.GenerateInitialPassword(
            FullName.Create(firstLoginUserRequest.FullName),
            firstLoginUserRequest.BirthDate);
        var firstLogin = await client.LoginAndAttachBearerTokenAsync(createdUser.Email, initialPassword);
        Assert.True(firstLogin.MustChangePassword);

        var blockedResponse = await client.GetAsync("/customers?page=1&pageSize=10");
        HttpResponseAssertions.AssertStatus(blockedResponse, HttpStatusCode.Forbidden);

        var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: initialPassword,
                NewPassword: "First.Login.Active#123"));
        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);

        var activeLogin = await client.LoginAndAttachBearerTokenAsync(
            createdUser.Email,
            "First.Login.Active#123");
        Assert.False(activeLogin.MustChangePassword);

        var unblockedResponse = await client.GetAsync("/customers?page=1&pageSize=10");
        HttpResponseAssertions.AssertStatus(unblockedResponse, HttpStatusCode.OK);
    }

    private static async Task<LoginResponse> AuthenticateCreatedUserAsActiveAsync(
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
        return activeLogin;
    }
}
