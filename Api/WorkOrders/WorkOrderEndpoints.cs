using GarageFlow.Api.Security;
using GarageFlow.Api.WorkOrders.AddEstimateInventoryItem;
using GarageFlow.Api.WorkOrders.AddEstimateService;
using GarageFlow.Api.WorkOrders.ApproveMyEstimate;
using GarageFlow.Api.WorkOrders.CancelWorkOrder;
using GarageFlow.Api.WorkOrders.CompleteWorkOrder;
using GarageFlow.Api.WorkOrders.CreateEstimate;
using GarageFlow.Api.WorkOrders.CreateWorkOrder;
using GarageFlow.Api.WorkOrders.DeliverWorkOrder;
using GarageFlow.Api.WorkOrders.GetMyWorkOrderById;
using GarageFlow.Api.WorkOrders.GetWorkOrderById;
using GarageFlow.Api.WorkOrders.ListMyWorkOrders;
using GarageFlow.Api.WorkOrders.ListWorkOrders;
using GarageFlow.Api.WorkOrders.RejectMyEstimate;
using GarageFlow.Api.WorkOrders.StartDiagnosis;
using GarageFlow.Api.WorkOrders.StartWork;
using GarageFlow.Api.WorkOrders.SubmitEstimate;

namespace GarageFlow.Api.WorkOrders;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var staffRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveStaff);

        staffRoutes.MapCreateWorkOrderEndpoint();
        staffRoutes.MapListWorkOrdersEndpoint();
        staffRoutes.MapGetWorkOrderByIdEndpoint();
        staffRoutes.MapCreateEstimateEndpoint();
        staffRoutes.MapAddEstimateInventoryItemEndpoint();
        staffRoutes.MapAddEstimateServiceEndpoint();
        staffRoutes.MapSubmitEstimateEndpoint();
        staffRoutes.MapStartDiagnosisEndpoint();
        staffRoutes.MapStartWorkEndpoint();
        staffRoutes.MapCompleteWorkOrderEndpoint();
        staffRoutes.MapDeliverWorkOrderEndpoint();
        staffRoutes.MapCancelWorkOrderEndpoint();

        var customerRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveCustomer);

        customerRoutes.MapListMyWorkOrdersEndpoint();
        customerRoutes.MapGetMyWorkOrderByIdEndpoint();
        customerRoutes.MapApproveMyEstimateEndpoint();
        customerRoutes.MapRejectMyEstimateEndpoint();

        return app;
    }
}
