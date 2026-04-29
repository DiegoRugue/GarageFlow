using System.Globalization;
using GarageFlow.BuildingBlocks.Domain.Exceptions;

namespace GarageFlow.Domain.Users.ValueObjects;

public readonly record struct UserBirthDate
{
    public DateOnly Value { get; }

    private UserBirthDate(DateOnly value)
    {
        Value = value;
    }

    public static UserBirthDate Create(DateOnly value)
    {
        if (value == default || value == DateOnly.MinValue)
        {
            throw new ValidationException("Birth date cannot be empty.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (value > today)
        {
            throw new ValidationException("Birth date cannot be in the future.");
        }

        return new UserBirthDate(value);
    }

    public override string ToString() => Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static implicit operator DateOnly(UserBirthDate birthDate) => birthDate.Value;
}
