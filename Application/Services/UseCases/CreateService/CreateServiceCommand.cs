using Mediator;

namespace GarageFlow.Application.Services.UseCases.CreateService;

public sealed record CreateServiceCommand(
    string Description,
    decimal Price) : IRequest<CreateServiceResult>;

