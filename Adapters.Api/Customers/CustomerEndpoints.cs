using GarageFlow.Adapters.Api.Customers.ActivateCustomerPortalUser;
using GarageFlow.Adapters.Api.Customers.ChangeCustomerStatus;
using GarageFlow.Adapters.Api.Customers.CreateCustomer;
using GarageFlow.Adapters.Api.Customers.DeleteCustomer;
using GarageFlow.Adapters.Api.Customers.GetCustomerById;
using GarageFlow.Adapters.Api.Customers.ListCustomers;
using GarageFlow.Adapters.Api.Customers.UpdateCustomer;
using GarageFlow.Adapters.Api.Security;

namespace GarageFlow.Adapters.Api.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var protectedCustomerRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveStaff);

        protectedCustomerRoutes.MapCreateCustomerEndpoint();
        protectedCustomerRoutes.MapActivateCustomerPortalUserEndpoint();
        protectedCustomerRoutes.MapChangeCustomerStatusEndpoint();
        protectedCustomerRoutes.MapGetCustomerByIdEndpoint();
        protectedCustomerRoutes.MapListCustomersEndpoint();
        protectedCustomerRoutes.MapUpdateCustomerEndpoint();
        protectedCustomerRoutes.MapDeleteCustomerEndpoint();
        return app;
    }
}
