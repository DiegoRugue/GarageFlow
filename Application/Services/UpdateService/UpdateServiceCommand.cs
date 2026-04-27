using Mediator;

namespace GarageFlow.Application.Services.UpdateService;

public sealed record UpdateServiceCommand(
    Guid Id,
    string Description,
    decimal Price) : IRequest<UpdateServiceResult>;

