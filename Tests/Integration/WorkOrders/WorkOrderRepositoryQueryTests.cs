using System.Linq.Expressions;
using System.Reflection;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Infrastructure.DataAccess;
using GarageFlow.Infrastructure.WorkOrders.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Tests.Integration.WorkOrders;

public sealed class WorkOrderRepositoryQueryTests
{
    [Fact]
    public void AggregateQuery_ShouldUseSplitQuery_ToAvoidCartesianMultiplication()
    {
        using var dbContext = CreateDbContext();
        var repository = new WorkOrderRepository(dbContext);
        var method = typeof(WorkOrderRepository).GetMethod(
            "CreateWorkOrderAggregateQuery",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        var query = Assert.IsType<IQueryable<WorkOrder>>(method!.Invoke(repository, null), exactMatch: false);

        Assert.True(ContainsAsSplitQueryCall(query.Expression));
    }

    [Fact]
    public async Task ListDetailsAsync_ShouldReturnExpectedLineCounts_ForWorkOrderWithMultipleLines()
    {
        using var dbContext = CreateDbContext();
        var repository = new WorkOrderRepository(dbContext);

        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var inventoryItemIdOne = InventoryItemId.New();
        var inventoryItemIdTwo = InventoryItemId.New();
        var serviceIdOne = ServiceId.New();
        var serviceIdTwo = ServiceId.New();

        var workOrder = WorkOrder.Create(customerId, vehicleId);
        var estimate = workOrder.CreateEstimate();

        workOrder.AddInventoryLine(
            estimate.Id,
            inventoryItemIdOne,
            Description.Create("Brake pads"),
            EstimateItemQuantity.Create(2),
            Price.Create(10.00m),
            Price.Create(25.00m));
        workOrder.AddInventoryLine(
            estimate.Id,
            inventoryItemIdTwo,
            Description.Create("Oil filter"),
            EstimateItemQuantity.Create(1),
            Price.Create(8.00m),
            Price.Create(18.00m));
        workOrder.AddServiceLine(
            estimate.Id,
            serviceIdOne,
            Description.Create("Oil change labor"),
            Price.Create(50.00m));
        workOrder.AddServiceLine(
            estimate.Id,
            serviceIdTwo,
            Description.Create("Brake inspection"),
            Price.Create(35.00m));

        await repository.AddAsync(workOrder);
        await dbContext.SaveChangesAsync();

        var (items, totalCount) = await repository.ListDetailsAsync(page: 1, pageSize: 10, customerId: customerId);

        Assert.Equal(1, totalCount);

        var detail = Assert.Single(items);
        var detailEstimate = Assert.Single(detail.Estimates);

        Assert.Equal(2, detailEstimate.InventoryLines.Count);
        Assert.Equal(2, detailEstimate.ServiceLines.Count);
    }

    private static GarageFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase($"garageflow-workorders-query-tests-{Guid.NewGuid():N}")
            .Options;

        return new GarageFlowDbContext(options);
    }

    private static bool ContainsAsSplitQueryCall(Expression expression)
    {
        var visitor = new AsSplitQueryExpressionVisitor();
        visitor.Visit(expression);
        return visitor.Found;
    }

    private sealed class AsSplitQueryExpressionVisitor : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (Found || node is null)
            {
                return node;
            }

            return base.Visit(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == nameof(RelationalQueryableExtensions.AsSplitQuery) &&
                node.Method.DeclaringType == typeof(RelationalQueryableExtensions))
            {
                Found = true;
                return node;
            }

            return base.VisitMethodCall(node);
        }
    }
}
