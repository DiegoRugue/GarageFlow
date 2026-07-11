namespace GarageFlow.Adapters.Api.Customers.UpdateCustomer;

public sealed record UpdateCustomerRequest(
    string FullName,
    string Email,
    string PhoneNumber);
