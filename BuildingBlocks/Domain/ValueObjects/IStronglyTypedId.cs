namespace GarageFlow.BuildingBlocks.Domain.ValueObjects;

public interface IStronglyTypedId
{
    Guid Value { get; }
}
