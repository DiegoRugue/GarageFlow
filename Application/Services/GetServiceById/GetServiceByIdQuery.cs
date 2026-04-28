using Mediator;

namespace GarageFlow.Application.Services.GetServiceById;

public sealed record GetServiceByIdQuery(Guid Id) : IRequest<ServiceDto>;
