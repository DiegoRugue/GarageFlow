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
        var activeLoginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                Email: IntegrationTestAuthSettings.BootstrapAdminEmail,
                Password: IntegrationTestAuthSettings.BootstrapAdminActivePassword));

        if (activeLoginResponse.IsSuccessStatusCode)
        {
            var activeLogin = await HttpResponseAssertions.ReadRequiredJsonAsync<LoginResponse>(activeLoginResponse);
            client.AttachBearerToken(activeLogin.Token);
            return activeLogin;
        }

        if (activeLoginResponse.StatusCode != HttpStatusCode.Unauthorized)
        {
            var activeLoginBody = await activeLoginResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Bootstrap admin active-password login failed with status {(int)activeLoginResponse.StatusCode} " +
                $"({activeLoginResponse.StatusCode}). Body: {activeLoginBody}");
        }

        var firstLogin = await client.LoginAndAttachBearerTokenAsync(
            IntegrationTestAuthSettings.BootstrapAdminEmail,
            IntegrationTestAuthSettings.BootstrapAdminInitialPassword);

        if (!firstLogin.MustChangePassword)
        {
            return firstLogin;
        }

        var changePasswordResponse = await client.PutAsJsonAsync(
            "/users/me/password",
            new ChangeMyPasswordRequest(
                CurrentPassword: IntegrationTestAuthSettings.BootstrapAdminInitialPassword,
                NewPassword: IntegrationTestAuthSettings.BootstrapAdminActivePassword));

        changePasswordResponse.EnsureSuccessStatusCode();

        return await client.LoginAndAttachBearerTokenAsync(
            IntegrationTestAuthSettings.BootstrapAdminEmail,
            IntegrationTestAuthSettings.BootstrapAdminActivePassword);
    }

    private sealed record LoginRequest(string Email, string Password);
}
