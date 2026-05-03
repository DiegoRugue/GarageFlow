using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Tests.Shared.WorkOrders;

public sealed class WorkOrderBuilder
{
    private readonly CustomerId _customerId = CustomerId.New();
    private readonly VehicleId _vehicleId = VehicleId.New();

    public WorkOrder BuildCreated()
    {
        return WorkOrder.Create(_customerId, _vehicleId);
    }

    public WorkOrder BuildWithPendingEstimate()
    {
        var workOrder = BuildCreated();
        var estimate = workOrder.CreateEstimate();
        AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);
        return workOrder;
    }

    public WorkOrder BuildWithApprovedEstimate()
    {
        var workOrder = BuildWithPendingEstimate();
        var estimateId = workOrder.Estimates.Single().Id;
        workOrder.ApproveEstimate(estimateId);
        return workOrder;
    }

    public WorkOrder BuildCompleted()
    {
        var workOrder = BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();
        workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
        workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);
        return workOrder;
    }

    public WorkOrder BuildDelivered()
    {
        var workOrder = BuildCompleted();
        workOrder.Deliver();
        return workOrder;
    }

    public WorkOrder BuildCancelled()
    {
        var workOrder = BuildCreated();
        workOrder.Cancel();
        return workOrder;
    }

    public static void AddDefaultInventoryLine(WorkOrder workOrder, EstimateId estimateId)
    {
        workOrder.AddInventoryLine(
            estimateId,
            InventoryItemId.New(),
            Description.Create("Brake pad kit"),
            EstimateItemQuantity.Create(2),
            Price.Create(40.00m),
            Price.Create(70.00m));
    }

    public static void AddDefaultServiceLine(WorkOrder workOrder, EstimateId estimateId)
    {
        workOrder.AddServiceLine(
            estimateId,
            ServiceId.New(),
            Description.Create("Diagnosis labor"),
            Price.Create(120.00m));
    }
}
