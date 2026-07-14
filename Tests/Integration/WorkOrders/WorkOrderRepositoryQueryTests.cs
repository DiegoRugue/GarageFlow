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
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Tests.Shared.WorkOrders;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Idempotency;
using GarageFlow.Adapters.Infrastructure.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Tests.Integration.WorkOrders;

public sealed class WorkOrderRepositoryQueryTests
{
    [Fact]
    public async Task IntakeRequestStore_ShouldAcquireThenReturnCompletedClaim_WhenUsingInMemorySequentially()
    {
        var databaseName = $"garageflow-intake-request-tests-{Guid.NewGuid():N}";
        var requestId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();
        var completedAt = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        await using (var firstContext = CreateDbContext(databaseName))
        {
            var store = new WorkOrderIntakeRequestStore(firstContext);
            var claim = await store.ClaimAsync(requestId, new string('a', 64));

            Assert.Equal(IntakeRequestClaimState.Acquired, claim.State);

            await store.CompleteAsync(requestId, workOrderId, "{\"status\":\"Received\"}", completedAt);
            await firstContext.SaveChangesAsync();
        }

        await using var secondContext = CreateDbContext(databaseName);
        var secondStore = new WorkOrderIntakeRequestStore(secondContext);

        var completedClaim = await secondStore.ClaimAsync(requestId, new string('a', 64));

        Assert.Equal(IntakeRequestClaimState.Completed, completedClaim.State);
        Assert.Equal(new string('a', 64), completedClaim.PayloadHash);
        Assert.Equal("{\"status\":\"Received\"}", completedClaim.ResponseJson);
    }

    [Fact]
    public async Task VehicleReferenceLookups_ShouldBeCaseInsensitiveAndTracked_WhenUsingInMemory()
    {
        await using var dbContext = CreateDbContext();
        var brand = VehicleBrand.Create("Honda");
        var model = VehicleModel.Create(brand.Id, "Civic");
        var color = VehicleColor.Create("Black");
        dbContext.AddRange(brand, model, color);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var foundBrand = await new VehicleBrandRepository(dbContext)
            .GetByNameAsync(VehicleBrandName.Create(" hONDa "));
        var foundModel = await new VehicleModelRepository(dbContext)
            .GetByNameAsync(brand.Id, VehicleModelName.Create(" cIVic "));
        var foundColor = await new VehicleColorRepository(dbContext)
            .GetByNameAsync(VehicleColorName.Create(" bLACK "));

        Assert.Equal(brand.Id, foundBrand?.Id);
        Assert.Equal(model.Id, foundModel?.Id);
        Assert.Equal(color.Id, foundColor?.Id);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(foundBrand!).State);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(foundModel!).State);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(foundColor!).State);
    }

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
    public async Task ListActiveDetailsAsync_ShouldReturnExpectedLineCounts_ForWorkOrderWithMultipleLines()
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

        var (items, totalCount) = await queries.ListActiveDetailsAsync(page: 1, pageSize: 10, customerId: customerId);

        Assert.Equal(1, totalCount);

        var detail = Assert.Single(items);
        var detailEstimate = Assert.Single(detail.Estimates);

        Assert.Equal(2, detailEstimate.InventoryLines.Count);
        Assert.Equal(2, detailEstimate.ServiceLines.Count);
    }

    [Fact]
    public async Task ListActiveDetailsAsync_ShouldFilterOrderAndPageActiveQueue_OnServerQuery()
    {
        using var dbContext = CreateDbContext();
        var customerId = CustomerId.New();
        var otherCustomerId = CustomerId.New();
        var oldest = new DateTime(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);
        var newest = oldest.AddHours(1);

        var inProgressNewest = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var inProgressOldest = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var waiting = new WorkOrderBuilder().BuildWithPendingEstimate();
        var diagnosing = WorkOrder.Create(customerId, VehicleId.New());
        diagnosing.StartDiagnosis();
        var receivedHigherId = WorkOrder.Create(customerId, VehicleId.New());
        var receivedLowerId = WorkOrder.Create(customerId, VehicleId.New());
        if (receivedHigherId.Id.Value.CompareTo(receivedLowerId.Id.Value) < 0)
        {
            (receivedHigherId, receivedLowerId) = (receivedLowerId, receivedHigherId);
        }

        var completed = new WorkOrderBuilder().BuildCompleted();
        var delivered = new WorkOrderBuilder().BuildDelivered();
        var cancelled = new WorkOrderBuilder().BuildCancelled();
        var otherCustomer = WorkOrder.Create(otherCustomerId, VehicleId.New());
        dbContext.WorkOrders.AddRange(
            inProgressNewest, inProgressOldest, waiting, diagnosing,
            receivedHigherId, receivedLowerId, completed, delivered, cancelled, otherCustomer);
        dbContext.Entry(inProgressNewest).Property(workOrder => workOrder.CustomerId).CurrentValue = customerId;
        dbContext.Entry(inProgressOldest).Property(workOrder => workOrder.CustomerId).CurrentValue = customerId;
        dbContext.Entry(waiting).Property(workOrder => workOrder.CustomerId).CurrentValue = customerId;
        SetCreatedAt(dbContext, inProgressNewest, newest);
        SetCreatedAt(dbContext, inProgressOldest, oldest);
        SetCreatedAt(dbContext, waiting, oldest);
        SetCreatedAt(dbContext, diagnosing, oldest);
        SetCreatedAt(dbContext, receivedHigherId, oldest);
        SetCreatedAt(dbContext, receivedLowerId, oldest);
        await dbContext.SaveChangesAsync();

        var queries = new EfWorkOrderQueries(dbContext);
        var (items, totalCount) = await queries.ListActiveDetailsAsync(1, 10);

        Assert.Equal(7, totalCount);
        Assert.Equal(
            [
                inProgressOldest.Id.Value,
                inProgressNewest.Id.Value,
                waiting.Id.Value,
                diagnosing.Id.Value,
                receivedLowerId.Id.Value,
                receivedHigherId.Id.Value,
                otherCustomer.Id.Value
            ],
            items.Select(item => item.Id));

        var (filteredItems, filteredCount) = await queries.ListActiveDetailsAsync(1, 3, customerId);
        Assert.Equal(6, filteredCount);
        Assert.Equal(
            [inProgressOldest.Id.Value, inProgressNewest.Id.Value, waiting.Id.Value],
            filteredItems.Select(item => item.Id));
    }

    [Fact]
    public async Task ListCustomerDetailsAsync_ShouldIncludeTerminalWorkOrders()
    {
        using var dbContext = CreateDbContext();
        var customerId = CustomerId.New();
        var completed = new WorkOrderBuilder().BuildCompleted();
        var delivered = new WorkOrderBuilder().BuildDelivered();
        var cancelled = WorkOrder.Create(customerId, VehicleId.New());
        cancelled.Cancel();
        dbContext.WorkOrders.AddRange(completed, delivered, cancelled);
        dbContext.Entry(completed).Property(workOrder => workOrder.CustomerId).CurrentValue = customerId;
        dbContext.Entry(delivered).Property(workOrder => workOrder.CustomerId).CurrentValue = customerId;
        await dbContext.SaveChangesAsync();

        var (items, totalCount) = await new EfWorkOrderQueries(dbContext)
            .ListCustomerDetailsAsync(1, 10, customerId);

        Assert.Equal(3, totalCount);
        Assert.Equal(
            [WorkOrderStatus.Cancelled.ToString(), WorkOrderStatus.Completed.ToString(), WorkOrderStatus.Delivered.ToString()],
            items.Select(item => item.Status).Order());
    }

    [Fact]
    public async Task GetStatusByIdAsync_ShouldReturnOnlyFocusedStatusProjection()
    {
        using var dbContext = CreateDbContext();
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());
        workOrder.StartDiagnosis();
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        var result = await new EfWorkOrderQueries(dbContext).GetStatusByIdAsync(workOrder.Id);

        Assert.NotNull(result);
        Assert.Equal(workOrder.Id.Value, result.Id);
        Assert.Equal("Diagnosing", result.Status);
        Assert.Equal(workOrder.UpdatedAt, result.UpdatedAt);
        Assert.Equal(["Id", "Status", "UpdatedAt"], result.GetType().GetProperties().Select(property => property.Name).Order());
    }

    private static GarageFlowDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"garageflow-workorders-query-tests-{Guid.NewGuid():N}")
            .Options;

        return new GarageFlowDbContext(options);
    }

    private static void SetCreatedAt(GarageFlowDbContext dbContext, WorkOrder workOrder, DateTime createdAt)
    {
        dbContext.Entry(workOrder).Property("CreatedAt").CurrentValue = createdAt;
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
