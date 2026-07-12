namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed record IntakeCustomerInput(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber);
