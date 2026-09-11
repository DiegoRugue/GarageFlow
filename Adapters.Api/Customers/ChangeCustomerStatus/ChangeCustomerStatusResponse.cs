namespace GarageFlow.Adapters.Api.Customers.ChangeCustomerStatus;

public sealed record ChangeCustomerStatusResponse(
    Guid Id,
    string Status);
