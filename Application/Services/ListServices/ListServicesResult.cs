using GarageFlow.Application.Services.GetServiceById;

namespace GarageFlow.Application.Services.ListServices;

public sealed record ListServicesResult(
    IReadOnlyList<ServiceDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

