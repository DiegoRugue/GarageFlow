using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.Common.Behaviors;
using GarageFlow.Application.Common.Events;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Tests.E2E.Support.Fixtures;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GarageFlow.Tests.E2E.WorkOrders;

public sealed class OutboxAtomicityPostgresE2eTests(E2eApiFixture fixture)
    : IClassFixture<E2eApiFixture>
{
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task TransactionBehavior_ShouldCommitDomainMutationAndOutboxTogether_InPostgreSql()
    {
        var dependencies = await CreateDependenciesAsync(
            taxDocument: "52998224725",
            email: "outbox-success@garageflow.local",
            phoneNumber: "11911111111",
            licensePlate: "OUT1B01");
        var dispatcher = new CapturingDispatcher();
        Guid workOrderId;

        using (var scope = _fixture.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
            var behavior = new TransactionBehavior<PersistWorkOrderCommand, Guid>(
                dbContext,
                dispatcher,
                new WorkOrderIntegrationOutboxMapper(),
                new EfOutboxWriter(dbContext));
            var workOrder = WorkOrder.Create(dependencies.CustomerId, dependencies.VehicleId);
            workOrderId = workOrder.Id.Value;
            MessageHandlerDelegate<PersistWorkOrderCommand, Guid> handler = (_, _) =>
            {
                dbContext.WorkOrders.Add(workOrder);
                workOrder.StartDiagnosis();
                return new ValueTask<Guid>(workOrderId);
            };

            var result = await behavior.Handle(new PersistWorkOrderCommand(), handler, CancellationToken.None);

            Assert.Equal(workOrderId, result);
        }

        using var verificationScope = _fixture.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        Assert.NotNull(await verificationContext.WorkOrders.AsNoTracking().SingleOrDefaultAsync(
            workOrder => workOrder.Id == GarageFlow.Domain.WorkOrders.ValueObjects.WorkOrderId.From(workOrderId)));
        var outbox = Assert.Single(await verificationContext.IntegrationOutboxMessages
            .AsNoTracking()
            .Where(message => message.AggregateId == workOrderId)
            .ToListAsync());
        Assert.Equal(WorkOrderStatusChangedIntegrationEvent.EventKey, outbox.EventKey);
        Assert.True(dispatcher.WasCalled);
    }

    [Fact]
    public async Task TransactionBehavior_ShouldRollbackDomainAndOutbox_WhenPostgreSqlSave2Fails()
    {
        var dependencies = await CreateDependenciesAsync(
            taxDocument: "11144477735",
            email: "outbox-rollback@garageflow.local",
            phoneNumber: "11922222222",
            licensePlate: "OUT2B02");
        var dispatcher = new CapturingDispatcher();
        Guid workOrderId;

        using (var scope = _fixture.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
            var writer = new Save2FailingOutboxWriter(dbContext);
            var behavior = new TransactionBehavior<PersistWorkOrderCommand, Guid>(
                dbContext,
                dispatcher,
                new WorkOrderIntegrationOutboxMapper(),
                writer);
            var workOrder = WorkOrder.Create(dependencies.CustomerId, dependencies.VehicleId);
            workOrderId = workOrder.Id.Value;
            MessageHandlerDelegate<PersistWorkOrderCommand, Guid> handler = (_, _) =>
            {
                dbContext.WorkOrders.Add(workOrder);
                workOrder.StartDiagnosis();
                return new ValueTask<Guid>(workOrderId);
            };

            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => behavior.Handle(new PersistWorkOrderCommand(), handler, CancellationToken.None).AsTask());
            var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, postgresException.SqlState);
            Assert.True(writer.WasCalledAfterSave1);
        }

        using var verificationScope = _fixture.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        Assert.False(await verificationContext.WorkOrders.AsNoTracking().AnyAsync(
            workOrder => workOrder.Id == GarageFlow.Domain.WorkOrders.ValueObjects.WorkOrderId.From(workOrderId)));
        Assert.False(await verificationContext.IntegrationOutboxMessages
            .AsNoTracking()
            .AnyAsync(message => message.AggregateId == workOrderId));
        Assert.False(dispatcher.WasCalled);
    }

    private async Task<Dependencies> CreateDependenciesAsync(
        string taxDocument,
        string email,
        string phoneNumber,
        string licensePlate)
    {
        using var scope = _fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var customer = Customer.Create(
            TaxDocument.Create(taxDocument),
            FullName.Create("Outbox Atomicity Customer"),
            Email.Create(email),
            PhoneNumber.Create(phoneNumber));
        var brand = VehicleBrand.Create($"Outbox Brand {licensePlate}");
        var model = VehicleModel.Create(brand.Id, $"Outbox Model {licensePlate}");
        var color = VehicleColor.Create($"Outbox Color {licensePlate}");
        var vehicle = Vehicle.Create(
            customer.Id,
            2025,
            brand.Id,
            model.Id,
            color.Id,
            LicensePlate.Create(licensePlate));

        dbContext.Customers.Add(customer);
        dbContext.VehicleBrands.Add(brand);
        dbContext.VehicleModels.Add(model);
        dbContext.VehicleColors.Add(color);
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        return new Dependencies(customer.Id, vehicle.Id);
    }

    private sealed record PersistWorkOrderCommand : GarageFlow.Application.Common.Messaging.ICommand<Guid>;

    private sealed record Dependencies(CustomerId CustomerId, VehicleId VehicleId);

    private sealed class CapturingDispatcher : IDomainEventDispatcher
    {
        public bool WasCalled { get; private set; }

        public ValueTask DispatchAsync(
            IReadOnlyCollection<DomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Save2FailingOutboxWriter(GarageFlowDbContext dbContext) : IOutboxWriter
    {
        private readonly EfOutboxWriter _writer = new(dbContext);

        public bool WasCalledAfterSave1 { get; private set; }

        public async Task WriteAsync(
            IReadOnlyCollection<IntegrationOutboxMessage> messages,
            CancellationToken cancellationToken)
        {
            WasCalledAfterSave1 = true;
            await _writer.WriteAsync(messages, cancellationToken);
            var message = Assert.Single(messages);
            dbContext.IntegrationOutboxMessages.Add(IntegrationOutboxMessageEntity.Create(
                Guid.NewGuid(),
                new string('x', 129),
                message.AggregateId,
                "{}",
                message.OccurredAt,
                message.CorrelationId));
        }
    }
}
