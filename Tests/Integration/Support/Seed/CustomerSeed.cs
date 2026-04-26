using System.Net.Http.Json;
using System.Text.Json;
using GarageFlow.Tests.Shared.Customers;

namespace GarageFlow.Tests.Integration.Support.Seed;

public static class CustomerSeed
{
    public static async Task<Guid> CreateIdAsync(
        HttpClient client,
        CustomerBuilder? builder = null)
    {
        var response = await client.PostAsJsonAsync(
            "/customers",
            (builder ?? new CustomerBuilder()).BuildCreateRequest());

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(contentStream);

        if (!document.RootElement.TryGetProperty("id", out var idElement) ||
            !idElement.TryGetGuid(out var customerId))
        {
            throw new InvalidOperationException("Create customer response did not contain a valid 'id' field.");
        }

        return customerId;
    }
}