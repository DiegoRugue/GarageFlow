namespace GarageFlow.Adapters.Api.Customers.GetCustomerById;

public sealed record CustomerResponse(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt,
    IReadOnlyList<CustomerVehicleResponse>? Vehicles = null);
