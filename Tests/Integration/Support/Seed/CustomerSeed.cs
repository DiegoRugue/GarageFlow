using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using GarageFlow.Tests.Shared.Customers;

namespace GarageFlow.Tests.Integration.Support.Seed;

public static class CustomerSeed
{
    private static int _taxDocumentSequence;

    public static CustomerBuilder CreateUniqueBuilder()
    {
        var taxDocument = NextTaxDocument();

        return new CustomerBuilder()
            .WithTaxDocument(taxDocument)
            .WithEmail($"customer.{taxDocument}@example.com");
    }

    public static async Task<Guid> CreateIdAsync(
        HttpClient client,
        CustomerBuilder? builder = null)
    {
        var effectiveBuilder = builder ?? CreateUniqueBuilder();

        var response = await client.PostAsJsonAsync(
            "/customers",
            effectiveBuilder.BuildCreateRequest());

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

    private static string NextTaxDocument()
    {
        var sequence = Interlocked.Increment(ref _taxDocumentSequence);
        var firstNineDigits = sequence
            .ToString("D9", CultureInfo.InvariantCulture)
            .Select(character => character - '0')
            .ToArray();

        var firstCheckDigit = CalculateCheckDigit(firstNineDigits, 10);
        var tenDigits = new int[10];
        Array.Copy(firstNineDigits, tenDigits, firstNineDigits.Length);
        tenDigits[9] = firstCheckDigit;

        var secondCheckDigit = CalculateCheckDigit(tenDigits, 11);

        return string.Concat(tenDigits.Select(digit => (char)('0' + digit))) + secondCheckDigit;
    }

    private static int CalculateCheckDigit(int[] digits, int initialWeight)
    {
        var sum = 0;
        for (var index = 0; index < digits.Length; index++)
        {
            sum += digits[index] * (initialWeight - index);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
