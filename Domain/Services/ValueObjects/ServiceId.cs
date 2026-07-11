using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Domain.Services.ValueObjects;

public readonly record struct ServiceId : IStronglyTypedId
{
    public Guid Value { get; }

    private ServiceId(Guid value)
    {
        Value = value;
    }

    public static ServiceId New() => new(Guid.NewGuid());

    public static ServiceId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("Service identifier cannot be empty.");
        }

        return new ServiceId(value);
    }

    public override string ToString() => Value.ToString();
}
