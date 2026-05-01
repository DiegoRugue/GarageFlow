using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public readonly record struct EstimateId : IStronglyTypedId
{
    public Guid Value { get; }

    private EstimateId(Guid value)
    {
        Value = value;
    }

    public static EstimateId New() => new(Guid.NewGuid());

    public static EstimateId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Estimate identifier cannot be empty.");
        }

        return new EstimateId(value);
    }

    public override string ToString() => Value.ToString();
}
