using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public readonly record struct EstimateServiceLineId : IStronglyTypedId
{
    public Guid Value { get; }

    private EstimateServiceLineId(Guid value)
    {
        Value = value;
    }

    public static EstimateServiceLineId New() => new(Guid.NewGuid());

    public static EstimateServiceLineId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Estimate service line identifier cannot be empty.");
        }

        return new EstimateServiceLineId(value);
    }

    public override string ToString() => Value.ToString();
}
