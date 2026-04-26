namespace GarageFlow.Application.Customers.GetCustomerById;

public sealed record CustomerDto(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt);
