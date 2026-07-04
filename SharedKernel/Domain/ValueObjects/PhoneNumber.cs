using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.SharedKernel.Domain.ValueObjects;

public sealed record PhoneNumber
{
    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Phone number cannot be empty.");
        }

        var digits = value
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("+", string.Empty)
            .Trim();

        if (!digits.All(char.IsDigit))
        {
            throw new ValidationException("Phone number must contain only digits after removing formatting characters.");
        }

        if (digits.Length != 10 && digits.Length != 11)
        {
            throw new ValidationException("Phone number must have 10 digits (landline) or 11 digits (mobile) after removing formatting.");
        }

        return new PhoneNumber(digits);
    }

    public override string ToString() => Value;

    public static implicit operator string(PhoneNumber phone) => phone.Value;
}
