namespace GarageFlow.Application.Customers.CreateCustomer;

public sealed record CreateCustomerResult(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt);
