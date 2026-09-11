namespace GarageFlow.Application.Customers.UseCases.ChangeCustomerStatus;

public sealed record ChangeCustomerStatusResult(
    Guid Id,
    string Status);
