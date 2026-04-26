using System.Net;
using System.Net.Http.Json;

namespace GarageFlow.Tests.Integration.Support.Helpers;

public static class HttpResponseAssertions
{
    public static void AssertStatus(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    public static async Task<TPayload> ReadRequiredJsonAsync<TPayload>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<TPayload>();
        Assert.NotNull(payload);

        return payload;
    }
}
