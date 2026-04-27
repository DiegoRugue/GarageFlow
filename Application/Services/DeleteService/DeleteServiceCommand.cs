using Mediator;

namespace GarageFlow.Application.Services.DeleteService;

public sealed record DeleteServiceCommand(Guid Id) : IRequest<Unit>;

