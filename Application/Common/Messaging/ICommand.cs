using Mediator;

namespace GarageFlow.Application.Common.Messaging;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface ICommand : ICommand<Unit>;
