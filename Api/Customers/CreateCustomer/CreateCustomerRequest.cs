namespace GarageFlow.Api.Customers.CreateCustomer;

public sealed record CreateCustomerRequest(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber);
