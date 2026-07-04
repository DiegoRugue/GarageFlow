using Mediator;

namespace GarageFlow.Application.Common.Messaging;

/// <summary>
/// Temporary opt-out for commands that still control their own transaction timing.
/// </summary>
public interface IManualTransactionCommand : IManualTransactionCommand<Unit>;
