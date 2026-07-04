using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.SharedKernel.Domain.ValueObjects;

public enum TaxDocumentType
{
    Cpf,
    Cnpj
}

public sealed record TaxDocument
{
    public string Value { get; }
    public TaxDocumentType DocumentType { get; }

    private TaxDocument(string value, TaxDocumentType type)
    {
        Value = value;
        DocumentType = type;
    }

    public static TaxDocument Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Tax document cannot be empty.");
        }

        var digits = value.Replace(".", string.Empty).Replace("-", string.Empty).Replace("/", string.Empty).Trim();

        if (digits.Length == 11)
        {
            if (!IsValidCpf(digits))
            {
                throw new ValidationException("Invalid CPF.");
            }

            return new TaxDocument(digits, TaxDocumentType.Cpf);
        }

        if (digits.Length == 14)
        {
            if (!IsValidCnpj(digits))
            {
                throw new ValidationException("Invalid CNPJ.");
            }

            return new TaxDocument(digits, TaxDocumentType.Cnpj);
        }

        throw new ValidationException("Tax document must be a valid CPF (11 digits) or CNPJ (14 digits).");
    }

    private static bool IsValidCpf(string digits)
    {
        if (!digits.All(char.IsDigit) || digits.Distinct().Count() == 1)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += (digits[i] - '0') * (10 - i);
        }

        var remainder = sum % 11;
        var firstDigit = remainder < 2 ? 0 : 11 - remainder;

        if ((digits[9] - '0') != firstDigit)
        {
            return false;
        }

        sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += (digits[i] - '0') * (11 - i);
        }

        remainder = sum % 11;
        var secondDigit = remainder < 2 ? 0 : 11 - remainder;

        return (digits[10] - '0') == secondDigit;
    }

    private static bool IsValidCnpj(string digits)
    {
        if (!digits.All(char.IsDigit) || digits.Distinct().Count() == 1)
        {
            return false;
        }

        int[] firstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (digits[i] - '0') * firstWeights[i];
        }

        var remainder = sum % 11;
        var firstDigit = remainder < 2 ? 0 : 11 - remainder;

        if ((digits[12] - '0') != firstDigit)
        {
            return false;
        }

        int[] secondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        sum = 0;
        for (var i = 0; i < 13; i++)
        {
            sum += (digits[i] - '0') * secondWeights[i];
        }

        remainder = sum % 11;
        var secondDigit = remainder < 2 ? 0 : 11 - remainder;

        return (digits[13] - '0') == secondDigit;
    }

    public override string ToString() => Value;

    public static implicit operator string(TaxDocument document) => document.Value;
}
