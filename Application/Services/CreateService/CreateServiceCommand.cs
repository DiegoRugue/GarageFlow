using Mediator;

namespace GarageFlow.Application.Services.CreateService;

public sealed record CreateServiceCommand(
    string Description,
    decimal Price) : IRequest<CreateServiceResult>;

