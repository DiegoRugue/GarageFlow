using Mediator;

namespace GarageFlow.Application.Services.ListServices;

public sealed record ListServicesQuery(int Page = 1, int PageSize = 20) : IRequest<ListServicesResult>;

