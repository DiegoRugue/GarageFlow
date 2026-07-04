using Mediator;

namespace GarageFlow.Application.Services.UseCases.DeleteService;

public sealed record DeleteServiceCommand(Guid Id) : IRequest<Unit>;

