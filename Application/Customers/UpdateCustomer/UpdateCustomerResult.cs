namespace GarageFlow.Application.Customers.UpdateCustomer;

public sealed record UpdateCustomerResult(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt);
