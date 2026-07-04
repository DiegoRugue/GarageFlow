using Mediator;

namespace GarageFlow.Application.Services.UseCases.UpdateService;

public sealed record UpdateServiceCommand(
    Guid Id,
    string Description,
    decimal Price) : IRequest<UpdateServiceResult>;

