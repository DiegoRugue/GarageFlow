using System.Text;
using System.Text.RegularExpressions;
using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Domain.Vehicles.ValueObjects;

public sealed partial record LicensePlate
{
    private static readonly Regex OldPlatePattern = OldPlateRegex();
    private static readonly Regex MercosulPlatePattern = MercosulPlateRegex();

    public string Value { get; }

    private LicensePlate(string value)
    {
        Value = value;
    }

    public static LicensePlate Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("License plate cannot be empty.");
        }

        var normalizedValue = Normalize(value);

        if (!IsValidFormat(normalizedValue))
        {
            throw new ValidationException("License plate must be a valid Brazilian format (ABC1234 or ABC1D23).");
        }

        return new LicensePlate(normalizedValue);
    }

    private static string Normalize(string value)
    {
        var normalizedBuilder = new StringBuilder(value.Length);

        foreach (var character in value.Trim())
        {
            if (character == '-' || char.IsWhiteSpace(character))
            {
                continue;
            }

            normalizedBuilder.Append(char.ToUpperInvariant(character));
        }

        return normalizedBuilder.ToString();
    }

    private static bool IsValidFormat(string value)
    {
        if (value.Length != 7 || value.Any(character => !char.IsLetterOrDigit(character)))
        {
            return false;
        }

        return OldPlatePattern.IsMatch(value) || MercosulPlatePattern.IsMatch(value);
    }

    public override string ToString() => Value;

    public static implicit operator string(LicensePlate plate) => plate.Value;

    [GeneratedRegex("^[A-Z]{3}[0-9]{4}$")]
    private static partial Regex OldPlateRegex();

    [GeneratedRegex("^[A-Z]{3}[0-9][A-Z][0-9]{2}$")]
    private static partial Regex MercosulPlateRegex();
}
