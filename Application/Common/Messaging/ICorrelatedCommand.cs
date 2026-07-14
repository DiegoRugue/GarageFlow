namespace GarageFlow.Application.Common.Messaging;

public interface ICorrelatedCommand
{
    string CorrelationId { get; }
}
