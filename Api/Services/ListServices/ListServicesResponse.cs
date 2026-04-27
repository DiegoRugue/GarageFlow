using GarageFlow.Api.Services.GetServiceById;

namespace GarageFlow.Api.Services.ListServices;

public sealed record ListServicesResponse(
    IReadOnlyList<ServiceResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

