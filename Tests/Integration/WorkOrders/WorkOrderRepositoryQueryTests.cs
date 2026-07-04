using System.Linq.Expressions;
using System.Reflection;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Tests.Integration.WorkOrders;

public sealed class WorkOrderRepositoryQueryTests
{
    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldReturnZeroCountAndNullAverage_WhenNoServiceLinesMatchWindow()
    {
        using var dbContext = CreateDbContext();
        var queries = new EfWorkOrderQueries(dbContext);

        var from = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

        var result = await queries.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(0, result.CompletedServicesCount);
        Assert.Null(result.AverageDurationMinutes);
    }

    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldIncludeCompletedAtBoundaries()
    {
        using var dbContext = CreateDbContext();
        var queries = new EfWorkOrderQueries(dbContext);

        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var serviceId = ServiceId.New();
        var from = new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc);

        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, from.AddMinutes(-30), from);
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, to.AddMinutes(-45), to);
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, from, from.AddMinutes(30));
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, from.AddHours(-1), from.AddTicks(-1));
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, to, to.AddTicks(1));
        await dbContext.SaveChangesAsync();

        var result = await queries.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(3, result.CompletedServicesCount);
    }

    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldExcludeRowsWithNullStartedAtOrCompletedAt()
    {
        using var dbContext = CreateDbContext();
        var queries = new EfWorkOrderQueries(dbContext);

        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var serviceId = ServiceId.New();
        var from = new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 3, 23, 59, 59, DateTimeKind.Utc);

        CreateWorkOrderWithServiceStatusAndTiming(
            dbContext,
            customerId,
            vehicleId,
            serviceId,
            startedAt: null,
            completedAt: from.AddHours(2));
        CreateWorkOrderWithServiceStatusAndTiming(
            dbContext,
            customerId,
            vehicleId,
            serviceId,
            startedAt: from.AddHours(3),
            completedAt: null);
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, from.AddHours(4), from.AddHours(5));
        await dbContext.SaveChangesAsync();

        var result = await queries.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(1, result.CompletedServicesCount);
        Assert.Equal(60d, result.AverageDurationMinutes);
    }

    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldComputeAverageFromCompletedAtMinusStartedAt()
    {
        using var dbContext = CreateDbContext();
        var queries = new EfWorkOrderQueries(dbContext);

        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var serviceId = ServiceId.New();
        var from = new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 6, 23, 59, 59, DateTimeKind.Utc);

        var firstCompletedAt = from.AddHours(8);
        var secondCompletedAt = from.AddHours(10);
        var thirdCompletedAt = from.AddHours(12);
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, firstCompletedAt.AddMinutes(-30), firstCompletedAt);
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, secondCompletedAt.AddMinutes(-45), secondCompletedAt);
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, serviceId, thirdCompletedAt.AddMinutes(-105), thirdCompletedAt);
        await dbContext.SaveChangesAsync();

        var result = await queries.GetAverageServiceTimeAsync(from, to);

        Assert.Equal(3, result.CompletedServicesCount);
        Assert.Equal(60d, result.AverageDurationMinutes);
    }

    [Fact]
    public async Task GetAverageServiceTimeAsync_ShouldFilterCompletedServiceLinesByServiceId()
    {
        using var dbContext = CreateDbContext();
        var queries = new EfWorkOrderQueries(dbContext);
        var customerId = CustomerId.New();
        var vehicleId = VehicleId.New();
        var targetServiceId = ServiceId.New();
        var otherServiceId = ServiceId.New();
        var from = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 2, 23, 59, 59, DateTimeKind.Utc);
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, targetServiceId, from.AddHours(8), from.AddHours(9));
        CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, otherServiceId, from.AddHours(10), from.AddHours(12));
        await dbContext.SaveChangesAsync();

        var result = await queries.GetAverageServiceTimeAsync(from, to, targetServiceId);

        Assert.Equal(1, result.CompletedServicesCount);
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
        var queries = new EfWorkOrderQueries(dbContext);

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

        var (items, totalCount) = await queries.ListDetailsAsync(page: 1, pageSize: 10, customerId: customerId);

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

    private static WorkOrder CreateWorkOrderWithCompletedService(
        GarageFlowDbContext dbContext,
        CustomerId customerId,
        VehicleId vehicleId,
        ServiceId serviceId,
        DateTime startedAt,
        DateTime completedAt)
    {
        return CreateWorkOrderWithServiceStatusAndTiming(
            dbContext,
            customerId,
            vehicleId,
            serviceId,
            startedAt,
            completedAt);
    }

    private static WorkOrder CreateWorkOrderWithServiceStatusAndTiming(
        GarageFlowDbContext dbContext,
        CustomerId customerId,
        VehicleId vehicleId,
        ServiceId serviceId,
        DateTime? startedAt,
        DateTime? completedAt)
    {
        var workOrder = WorkOrder.Create(customerId, vehicleId);
        var estimate = workOrder.CreateEstimate();
        workOrder.AddServiceLine(
            estimate.Id,
            serviceId,
            Description.Create("Timed service"),
            Price.Create(100m));
        workOrder.SubmitEstimate(estimate.Id);
        workOrder.ApproveEstimate(estimate.Id);
        var serviceLine = estimate.ServiceLines.Single();
        workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
        if (completedAt.HasValue)
        {
            workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);
        }

        dbContext.WorkOrders.Add(workOrder);
        dbContext.Entry(serviceLine).Property(line => line.StartedAt).CurrentValue = startedAt;
        dbContext.Entry(serviceLine).Property(line => line.CompletedAt).CurrentValue = completedAt;
        dbContext.Entry(workOrder).Property(item => item.StartedAt).CurrentValue = startedAt;
        dbContext.Entry(workOrder).Property(item => item.CompletedAt).CurrentValue = completedAt;
        return workOrder;
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
