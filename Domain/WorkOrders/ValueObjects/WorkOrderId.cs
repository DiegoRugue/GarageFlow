using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public readonly record struct WorkOrderId : IStronglyTypedId
{
    public Guid Value { get; }

    private WorkOrderId(Guid value)
    {
        Value = value;
    }

    public static WorkOrderId New() => new(Guid.NewGuid());

    public static WorkOrderId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Work order identifier cannot be empty.");
        }

        return new WorkOrderId(value);
    }

    public override string ToString() => Value.ToString();
}
