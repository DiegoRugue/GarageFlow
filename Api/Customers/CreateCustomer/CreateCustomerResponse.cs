namespace GarageFlow.Api.Customers.CreateCustomer;

public sealed record CreateCustomerResponse(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt);
