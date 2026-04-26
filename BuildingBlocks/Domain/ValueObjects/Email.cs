using GarageFlow.BuildingBlocks.Domain.Exceptions;

namespace GarageFlow.BuildingBlocks.Domain.ValueObjects;

public sealed record Email
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Email address cannot be empty or whitespace.");
        }

        var trimmedValue = value.Trim();

        if (!IsValidEmailFormat(trimmedValue))
        {
            throw new ValidationException("Email address format is invalid.");
        }

        return new Email(trimmedValue);
    }

    private static bool IsValidEmailFormat(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;
}
