namespace GarageFlow.Application.Customers.UseCases.CreateCustomer;

public sealed record CreateCustomerResult(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt);
