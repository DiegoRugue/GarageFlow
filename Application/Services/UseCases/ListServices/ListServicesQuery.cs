using Mediator;

namespace GarageFlow.Application.Services.UseCases.ListServices;

public sealed record ListServicesQuery(int Page = 1, int PageSize = 20) : IRequest<ListServicesResult>;

