using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GarageFlow.Tests.E2E.Support.Contracts.Auth;

namespace GarageFlow.Tests.E2E.Support.Helpers;

public static class AuthHttpClientExtensions
{
    public static async Task<LoginResponse> LoginAsync(this HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(Email: email, Password: password));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Login failed with status {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
        }

        return await HttpResponseAssertions.ReadRequiredJsonAsync<LoginResponse>(response);
    }

    public static async Task<LoginResponse> LoginAndAttachBearerTokenAsync(this HttpClient client, string email, string password)
    {
        var login = await client.LoginAsync(email, password);
        client.AttachBearerToken(login.Token);
        return login;
    }

    public static void AttachBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<LoginResponse> AuthenticateAsActiveBootstrapAdminAsync(this HttpClient client)
    {
        var activeLogin = await TryBootstrapAdminLoginAsync(client, E2eAuthSettings.BootstrapAdminActivePassword);
        if (activeLogin is not null)
        {
            client.AttachBearerToken(activeLogin.Token);
            return activeLogin;
        }

        var firstLogin = await TryBootstrapAdminLoginAsync(client, E2eAuthSettings.BootstrapAdminInitialPassword);
        if (firstLogin is null)
        {
            var retryActiveLogin = await TryBootstrapAdminLoginAsync(client, E2eAuthSettings.BootstrapAdminActivePassword);
            if (retryActiveLogin is not null)
            {
                client.AttachBearerToken(retryActiveLogin.Token);
                return retryActiveLogin;
            }

            throw new HttpRequestException(
                "Bootstrap admin login failed for both initial and active passwords.");
        }

        client.AttachBearerToken(firstLogin.Token);

        if (!firstLogin.MustChangePassword)
        {
            return firstLogin;
        }

        using var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: E2eAuthSettings.BootstrapAdminInitialPassword,
                NewPassword: E2eAuthSettings.BootstrapAdminActivePassword));

        if (!changePasswordResponse.IsSuccessStatusCode)
        {
            if (changePasswordResponse.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Unauthorized)
            {
                var activeAfterConflict = await TryBootstrapAdminLoginAsync(client, E2eAuthSettings.BootstrapAdminActivePassword);
                if (activeAfterConflict is not null)
                {
                    client.AttachBearerToken(activeAfterConflict.Token);
                    return activeAfterConflict;
                }
            }

            var changePasswordBody = await changePasswordResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Bootstrap admin change-password failed with status {(int)changePasswordResponse.StatusCode} " +
                $"({changePasswordResponse.StatusCode}). Body: {changePasswordBody}");
        }

        return await client.LoginAndAttachBearerTokenAsync(
            E2eAuthSettings.BootstrapAdminEmail,
            E2eAuthSettings.BootstrapAdminActivePassword);
    }

    public static async Task<LoginResponse> AuthenticateAsActiveAttendantAsync(this HttpClient client, string uniqueSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uniqueSeed);

        var birthDate = new DateOnly(1994, 4, 4);
        var fullName = $"E2E Inventory {uniqueSeed[..8]} Runner";
        var email = $"e2e-inventory-attendant-{uniqueSeed}@garageflow.local";

        using var createUserResponse = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest(
                FullName: fullName,
                Email: email,
                BirthDate: birthDate,
                Role: (int)UserRole.Attendant));

        HttpResponseAssertions.AssertStatus(createUserResponse, HttpStatusCode.Created);

        var createdUser = await HttpResponseAssertions.ReadRequiredJsonAsync<CreateUserResponse>(createUserResponse);
        Assert.NotEqual(Guid.Empty, createdUser.Id);
        Assert.Equal(fullName, createdUser.FullName);
        Assert.Equal(email, createdUser.Email);
        Assert.Equal(birthDate, createdUser.BirthDate);
        Assert.Equal((int)UserRole.Attendant, createdUser.Role);
        Assert.NotEqual(default, createdUser.CreatedAt);

        var initialPassword = $"{AttendantPasswordPrefix}{birthDate.Year}";
        var activePassword = $"{AttendantActivePasswordPrefix}{uniqueSeed[..6]}";
        var login = await client.LoginAndAttachBearerTokenAsync(email, initialPassword);

        if (!login.MustChangePassword)
        {
            return login;
        }

        using var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: initialPassword,
                NewPassword: activePassword));

        HttpResponseAssertions.AssertStatus(changePasswordResponse, HttpStatusCode.NoContent);

        return await client.LoginAndAttachBearerTokenAsync(email, activePassword);
    }

    private static async Task<LoginResponse?> TryBootstrapAdminLoginAsync(HttpClient client, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                Email: E2eAuthSettings.BootstrapAdminEmail,
                Password: password));

        if (response.IsSuccessStatusCode)
        {
            return await HttpResponseAssertions.ReadRequiredJsonAsync<LoginResponse>(response);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Bootstrap admin login failed with status {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }

    private const string AttendantPasswordPrefix = "runner";
    private const string AttendantActivePasswordPrefix = "E2E.Attendant#";

    private enum UserRole
    {
        Attendant = 2
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);

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
}
