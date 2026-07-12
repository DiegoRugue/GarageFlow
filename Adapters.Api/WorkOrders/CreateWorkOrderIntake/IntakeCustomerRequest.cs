namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrderIntake;

public sealed record IntakeCustomerRequest(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber);
