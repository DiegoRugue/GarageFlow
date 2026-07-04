using GarageFlow.Adapters.Api.Services.GetServiceById;

namespace GarageFlow.Adapters.Api.Services.ListServices;

public sealed record ListServicesResponse(
    IReadOnlyList<ServiceResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

