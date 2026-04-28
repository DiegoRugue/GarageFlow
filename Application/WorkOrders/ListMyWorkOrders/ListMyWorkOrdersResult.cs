namespace GarageFlow.Application.WorkOrders.ListMyWorkOrders;

public sealed record ListMyWorkOrdersResult(
    IReadOnlyList<CustomerWorkOrderDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
