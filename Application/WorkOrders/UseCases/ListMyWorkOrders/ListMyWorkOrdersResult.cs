using GarageFlow.Application.WorkOrders.Common;
namespace GarageFlow.Application.WorkOrders.UseCases.ListMyWorkOrders;

public sealed record ListMyWorkOrdersResult(
    IReadOnlyList<CustomerWorkOrderDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
