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
    public async Task GetAverageServiceTimeAsync_ShouldReturnZeroCountAndNullAverage_WhenNoWorkOrdersMatchWindow()
    {
        using var dbContext = CreateDbContext();
        var repository = new WorkOrderRepository(dbContext);

        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

        var result = await repository.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(0, result.CompletedWorkOrdersCount);
        Assert.Null(result.AverageDurationMinutes);
    }

    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldIncludeCompletedAtBoundaries()
    {
        using var dbContext = CreateDbContext();
        var repository = new WorkOrderRepository(dbContext);

        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var from = new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc);

        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, from.AddMinutes(-30), from);
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, to.AddMinutes(-45), to);
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, from, from.AddMinutes(30));
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, from.AddHours(-1), from.AddTicks(-1));
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, to, to.AddTicks(1));
        await dbContext.SaveChangesAsync();

        var result = await repository.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(3, result.CompletedWorkOrdersCount);
    }

    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldExcludeRowsWithNullStartedAtOrCompletedAt()
    {
        using var dbContext = CreateDbContext();
        var repository = new WorkOrderRepository(dbContext);

        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var from = new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 3, 23, 59, 59, DateTimeKind.Utc);

        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, null, from.AddHours(2));
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, from.AddHours(3), null);
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, from.AddHours(4), from.AddHours(5));
        await dbContext.SaveChangesAsync();

        var result = await repository.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(1, result.CompletedWorkOrdersCount);
        Assert.Equal(60d, result.AverageDurationMinutes);
    }

    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldComputeAverageFromCompletedAtMinusStartedAt()
    {
        using var dbContext = CreateDbContext();
        var repository = new WorkOrderRepository(dbContext);

        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var from = new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 6, 23, 59, 59, DateTimeKind.Utc);

        var firstCompletedAt = from.AddHours(8);
        var secondCompletedAt = from.AddHours(10);
        var thirdCompletedAt = from.AddHours(12);
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, firstCompletedAt.AddMinutes(-30), firstCompletedAt);
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, secondCompletedAt.AddMinutes(-45), secondCompletedAt);
        CreateWorkOrderWithTiming(dbContext, customerId, vehicleId, thirdCompletedAt.AddMinutes(-105), thirdCompletedAt);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(3, result.CompletedWorkOrdersCount);
        Assert.Equal(60d, result.AverageDurationMinutes);
    }

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

    private static void CreateWorkOrderWithTiming(
        GarageFlowDbContext dbContext,
        CustomerId customerId,
        VehicleId vehicleId,
        DateTime? startedAt,
        DateTime? completedAt)
    {
        var workOrder = WorkOrder.Create(customerId, vehicleId);
        dbContext.WorkOrders.Add(workOrder);
        var entry = dbContext.Entry(workOrder);
        entry.Property(item => item.StartedAt).CurrentValue = startedAt;
        entry.Property(item => item.CompletedAt).CurrentValue = completedAt;
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
