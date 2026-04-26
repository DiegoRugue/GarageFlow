namespace GarageFlow.Api.Customers.UpdateCustomer;

public sealed record UpdateCustomerRequest(
    string FullName,
    string Email,
    string PhoneNumber);
