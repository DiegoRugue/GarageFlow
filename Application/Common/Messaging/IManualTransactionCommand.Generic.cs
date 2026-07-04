namespace GarageFlow.Application.Common.Messaging;

/// <summary>
/// Temporary opt-out for commands that still control their own transaction timing.
/// </summary>
public interface IManualTransactionCommand<out TResponse> : ICommand<TResponse>;
