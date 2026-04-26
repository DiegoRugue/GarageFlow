using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;

namespace GarageFlow.Domain.Customers.ValueObjects;

public readonly record struct CustomerId : IStronglyTypedId
{
    public Guid Value { get; }

    private CustomerId(Guid value)
    {
        Value = value;
    }

    public static CustomerId New() => new(Guid.NewGuid());

    public static CustomerId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Customer identifier cannot be empty.");
        }

        return new CustomerId(value);
    }

    public override string ToString() => Value.ToString();
}
