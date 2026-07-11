using Mediator;

namespace GarageFlow.Application.Services.UseCases.GetServiceById;

public sealed record GetServiceByIdQuery(Guid Id) : IRequest<ServiceDto>;
