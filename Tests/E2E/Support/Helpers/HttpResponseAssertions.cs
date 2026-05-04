using System.Net;
using System.Net.Http.Json;
using Xunit.Sdk;

namespace GarageFlow.Tests.E2E.Support.Helpers;

public static class HttpResponseAssertions
{
    public static void AssertStatus(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        if (response.StatusCode == expectedStatusCode)
        {
            return;
        }

        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        throw new XunitException(
            $"Expected HTTP {(int)expectedStatusCode} ({expectedStatusCode}) but got {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }

    public static async Task<TPayload> ReadRequiredJsonAsync<TPayload>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<TPayload>();
        Assert.NotNull(payload);
        return payload;
    }
}
