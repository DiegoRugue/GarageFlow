using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderDailyMetrics;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.WorkOrders;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GarageFlow.Tests.E2E.WorkOrders;

[Collection(E2eApiCollection.Name)]
public sealed class WorkOrderDailyMetricsPostgresE2eTests(E2eApiFixture fixture)
{
    private static readonly DateTime FromUtc = new(2042, 9, 13, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToUtc = new(2042, 9, 14, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DailyMetrics_ShouldAverageEachEligibleOrderOnceAndUseSeparateHalfOpenWindows()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (customerId, vehicleId) = await CreateDependenciesAsync(db);

        // 600 + 1800 seconds / two orders = 1200, irrespective of service line count.
        AddOrder(db, customerId, vehicleId, FromUtc.AddDays(-1), FromUtc.AddMinutes(-10), FromUtc, WorkOrderStatus.Completed, 3);
        AddOrder(db, customerId, vehicleId, FromUtc.AddDays(-2), FromUtc.AddHours(1), FromUtc.AddHours(1).AddMinutes(30), WorkOrderStatus.Delivered, 1);
        // Creation boundaries and a completion exactly at the exclusive upper bound.
        AddOrder(db, customerId, vehicleId, FromUtc, null, null, WorkOrderStatus.Received);
        AddOrder(db, customerId, vehicleId, ToUtc.AddTicks(-10), ToUtc.AddMinutes(-5), ToUtc, WorkOrderStatus.Completed);
        AddOrder(db, customerId, vehicleId, ToUtc, FromUtc.AddMinutes(-2), FromUtc.AddTicks(-10), WorkOrderStatus.Completed);
        foreach (var status in new[] { WorkOrderStatus.Received, WorkOrderStatus.Diagnosing, WorkOrderStatus.WaitingApproval, WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled })
        {
            AddOrder(db, customerId, vehicleId, FromUtc.AddDays(-1), FromUtc, FromUtc.AddHours(1), status);
        }
        AddOrder(db, customerId, vehicleId, FromUtc.AddDays(-1), null, FromUtc.AddHours(1), WorkOrderStatus.Completed);
        AddOrder(db, customerId, vehicleId, FromUtc.AddDays(-1), FromUtc, null, WorkOrderStatus.Completed);
        AddOrder(db, customerId, vehicleId, FromUtc.AddDays(-1), FromUtc.AddHours(2), FromUtc.AddHours(1), WorkOrderStatus.Completed);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new GetWorkOrderDailyMetricsQuery(new DateOnly(2042, 9, 13)));

        Assert.Equal(FromUtc, result.FromUtc);
        Assert.Equal(ToUtc, result.ToUtc);
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(2, result.CompletedCount);
        Assert.Equal(1200.0, result.AverageDurationSeconds);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DailyMetrics_ShouldReturnNullMeanWhenNoEligibleCompletionsExist()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var queries = scope.ServiceProvider.GetRequiredService<IWorkOrderMetricsQueries>();

        var empty = await queries.GetDailyAsync(FromUtc, ToUtc);
        Assert.Equal(0, empty.CreatedCount);
        Assert.Equal(0, empty.CompletedCount);
        Assert.Null(empty.AverageDurationSeconds);

        var (customerId, vehicleId) = await CreateDependenciesAsync(db);
        AddOrder(db, customerId, vehicleId, FromUtc, null, FromUtc.AddHours(1), WorkOrderStatus.Delivered);
        AddOrder(db, customerId, vehicleId, FromUtc, FromUtc.AddHours(2), FromUtc.AddHours(1), WorkOrderStatus.Completed);
        await db.SaveChangesAsync();

        var invalidOnly = await queries.GetDailyAsync(FromUtc, ToUtc);
        Assert.Equal(2, invalidOnly.CreatedCount);
        Assert.Equal(0, invalidOnly.CompletedCount);
        Assert.Null(invalidOnly.AverageDurationSeconds);
    }

    [Fact]
    public async Task DailyMetrics_ShouldKeepLegitimateZeroDuration()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (customerId, vehicleId) = await CreateDependenciesAsync(db);
        AddOrder(db, customerId, vehicleId, FromUtc.AddDays(-1), FromUtc, FromUtc, WorkOrderStatus.Delivered);
        await db.SaveChangesAsync();

        var result = await scope.ServiceProvider.GetRequiredService<IWorkOrderMetricsQueries>().GetDailyAsync(FromUtc, ToUtc);

        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(0.0, result.AverageDurationSeconds);
    }

    private static void AddOrder(GarageFlowDbContext db, CustomerId customerId, VehicleId vehicleId,
        DateTime createdAt, DateTime? startedAt, DateTime? completedAt, WorkOrderStatus status, int serviceLines = 0)
    {
        var order = WorkOrder.Create(customerId, vehicleId);
        if (serviceLines > 0)
        {
            var estimate = order.CreateEstimate();
            for (var i = 0; i < serviceLines; i++)
            {
                WorkOrderBuilder.AddDefaultServiceLine(order, estimate.Id);
            }
        }

        var entry = db.WorkOrders.Add(order);
        // Persist historical/corrupt records directly to exercise reporting eligibility independently of today's domain guards.
        entry.Property(order => order.CreatedAt).CurrentValue = createdAt;
        entry.Property(order => order.StartedAt).CurrentValue = startedAt;
        entry.Property(order => order.CompletedAt).CurrentValue = completedAt;
        entry.Property(order => order.Status).CurrentValue = status;
    }

    private static async Task<(CustomerId, VehicleId)> CreateDependenciesAsync(GarageFlowDbContext db)
    {
        var customer = await db.Customers.FirstOrDefaultAsync();
        if (customer is null)
        {
            customer = new CustomerBuilder().Build();
            db.Customers.Add(customer);
        }

        var brand = VehicleBrand.Create("Daily Metrics Brand");
        var model = VehicleModel.Create(brand.Id, "Daily Metrics Model");
        var color = VehicleColor.Create("Daily Metrics Color");
        var vehicle = Vehicle.Create(customer.Id, 2025, brand.Id, model.Id, color.Id, LicensePlate.Create("MET1R01"));
        db.VehicleBrands.Add(brand);
        db.VehicleModels.Add(model);
        db.VehicleColors.Add(color);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return (customer.Id, vehicle.Id);
    }
}
