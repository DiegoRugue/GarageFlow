using GarageFlow.Adapters.Api.Security;
using GarageFlow.Adapters.Api.WorkOrders.AddEstimateInventoryItem;
using GarageFlow.Adapters.Api.WorkOrders.AddEstimateService;
using GarageFlow.Adapters.Api.WorkOrders.ApproveMyEstimate;
using GarageFlow.Adapters.Api.WorkOrders.CancelWorkOrder;
using GarageFlow.Adapters.Api.WorkOrders.CompleteEstimateService;
using GarageFlow.Adapters.Api.WorkOrders.CreateEstimate;
using GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrder;
using GarageFlow.Adapters.Api.WorkOrders.DeliverWorkOrder;
using GarageFlow.Adapters.Api.WorkOrders.GetAverageServiceTime;
using GarageFlow.Adapters.Api.WorkOrders.GetMyWorkOrderById;
using GarageFlow.Adapters.Api.WorkOrders.GetWorkOrderById;
using GarageFlow.Adapters.Api.WorkOrders.ListMyWorkOrders;
using GarageFlow.Adapters.Api.WorkOrders.ListWorkOrders;
using GarageFlow.Adapters.Api.WorkOrders.RejectMyEstimate;
using GarageFlow.Adapters.Api.WorkOrders.StartDiagnosis;
using GarageFlow.Adapters.Api.WorkOrders.StartEstimateService;
using GarageFlow.Adapters.Api.WorkOrders.StartWork;
using GarageFlow.Adapters.Api.WorkOrders.SubmitEstimate;

namespace GarageFlow.Adapters.Api.WorkOrders;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var staffRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveStaff);

        staffRoutes.MapCreateWorkOrderEndpoint();
        staffRoutes.MapListWorkOrdersEndpoint();
        staffRoutes.MapGetAverageServiceTimeEndpoint();
        staffRoutes.MapGetWorkOrderByIdEndpoint();
        staffRoutes.MapCreateEstimateEndpoint();
        staffRoutes.MapAddEstimateInventoryItemEndpoint();
        staffRoutes.MapAddEstimateServiceEndpoint();
        staffRoutes.MapSubmitEstimateEndpoint();
        staffRoutes.MapStartDiagnosisEndpoint();
        staffRoutes.MapStartWorkEndpoint();
        staffRoutes.MapStartEstimateServiceEndpoint();
        staffRoutes.MapCompleteEstimateServiceEndpoint();
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
