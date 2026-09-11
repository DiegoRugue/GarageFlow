namespace GarageFlow.Application.Customers.UseCases.GetCustomerById;

public sealed record CustomerDto(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<CustomerVehicleDto>? Vehicles = null);
