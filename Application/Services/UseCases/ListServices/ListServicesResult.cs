using GarageFlow.Application.Services.UseCases.GetServiceById;

namespace GarageFlow.Application.Services.UseCases.ListServices;

public sealed record ListServicesResult(
    IReadOnlyList<ServiceDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

