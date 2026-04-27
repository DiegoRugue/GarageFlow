using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Auth.Contracts;
using GarageFlow.Tests.Shared.Users;

namespace GarageFlow.Tests.Integration.Support.Helpers;

public static class AuthHttpClientExtensions
{
    public static async Task<LoginResponse> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
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
        var activeLogin = await TryBootstrapAdminLoginAsync(client, IntegrationTestAuthSettings.BootstrapAdminActivePassword);
        if (activeLogin is not null)
        {
            client.AttachBearerToken(activeLogin.Token);
            return activeLogin;
        }

        var firstLogin = await TryBootstrapAdminLoginAsync(client, IntegrationTestAuthSettings.BootstrapAdminInitialPassword);
        if (firstLogin is null)
        {
            var retryActiveLogin = await TryBootstrapAdminLoginAsync(client, IntegrationTestAuthSettings.BootstrapAdminActivePassword);
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

        var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: IntegrationTestAuthSettings.BootstrapAdminInitialPassword,
                NewPassword: IntegrationTestAuthSettings.BootstrapAdminActivePassword));

        if (!changePasswordResponse.IsSuccessStatusCode)
        {
            if (changePasswordResponse.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Unauthorized)
            {
                var activeAfterConflict = await TryBootstrapAdminLoginAsync(client, IntegrationTestAuthSettings.BootstrapAdminActivePassword);
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
            IntegrationTestAuthSettings.BootstrapAdminEmail,
            IntegrationTestAuthSettings.BootstrapAdminActivePassword);
    }

    private static async Task<LoginResponse?> TryBootstrapAdminLoginAsync(HttpClient client, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                Email: IntegrationTestAuthSettings.BootstrapAdminEmail,
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

    private sealed record LoginRequest(string Email, string Password);
}
