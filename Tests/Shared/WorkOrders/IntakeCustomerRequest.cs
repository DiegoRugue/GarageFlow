namespace GarageFlow.Tests.Shared.WorkOrders;

public sealed record IntakeCustomerRequest(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber);
