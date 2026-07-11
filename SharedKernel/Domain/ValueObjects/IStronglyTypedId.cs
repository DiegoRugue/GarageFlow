namespace GarageFlow.SharedKernel.Domain.ValueObjects;

public interface IStronglyTypedId
{
    Guid Value { get; }
}
