using GarageFlow.Api.Customers.ActivateCustomerPortalUser;
using GarageFlow.Api.Customers.CreateCustomer;
using GarageFlow.Api.Customers.DeleteCustomer;
using GarageFlow.Api.Customers.GetCustomerById;
using GarageFlow.Api.Customers.ListCustomers;
using GarageFlow.Api.Customers.UpdateCustomer;
using GarageFlow.Api.Security;

namespace GarageFlow.Api.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var protectedCustomerRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveUser);

        protectedCustomerRoutes.MapCreateCustomerEndpoint();
        protectedCustomerRoutes.MapActivateCustomerPortalUserEndpoint();
        protectedCustomerRoutes.MapGetCustomerByIdEndpoint();
        protectedCustomerRoutes.MapListCustomersEndpoint();
        protectedCustomerRoutes.MapUpdateCustomerEndpoint();
        protectedCustomerRoutes.MapDeleteCustomerEndpoint();
        return app;
    }
}
