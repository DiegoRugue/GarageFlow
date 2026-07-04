using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Domain.Users.ValueObjects;

public readonly record struct UserId : IStronglyTypedId
{
    public Guid Value { get; }

    private UserId(Guid value)
    {
        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("User identifier cannot be empty.");
        }

        return new UserId(value);
    }

    public override string ToString() => Value.ToString();
}
