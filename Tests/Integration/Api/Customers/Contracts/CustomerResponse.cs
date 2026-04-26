namespace GarageFlow.Tests.Integration.Api.Customers.Contracts;

public sealed record CustomerResponse(
    Guid Id,
    string TaxDocument,
    string TaxDocumentType,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt,
    IReadOnlyList<CustomerVehicleResponse>? Vehicles = null);
