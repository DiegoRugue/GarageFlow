using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public readonly record struct EstimateInventoryLineId : IStronglyTypedId
{
    public Guid Value { get; }

    private EstimateInventoryLineId(Guid value)
    {
        Value = value;
    }

    public static EstimateInventoryLineId New() => new(Guid.NewGuid());

    public static EstimateInventoryLineId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Estimate inventory line identifier cannot be empty.");
        }

        return new EstimateInventoryLineId(value);
    }

    public override string ToString() => Value.ToString();
}
