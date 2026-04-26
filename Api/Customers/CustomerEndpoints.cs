using GarageFlow.Api.Customers.CreateCustomer;
using GarageFlow.Api.Customers.DeleteCustomer;
using GarageFlow.Api.Customers.GetCustomerById;
using GarageFlow.Api.Customers.ListCustomers;
using GarageFlow.Api.Customers.UpdateCustomer;

namespace GarageFlow.Api.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateCustomerEndpoint();
        app.MapGetCustomerByIdEndpoint();
        app.MapListCustomersEndpoint();
        app.MapUpdateCustomerEndpoint();
        app.MapDeleteCustomerEndpoint();
        return app;
    }
}
