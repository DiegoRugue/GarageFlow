# Work Order Service State Machine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move work order completion to service-line execution, add start/complete service endpoints, mock customer approval email logging, and calculate average duration from completed service lines with optional `serviceId` filtering.

**Architecture:** Keep lifecycle rules inside the `WorkOrder` aggregate and `EstimateServiceLine` child entity. Add thin Minimal API endpoints that send Mediator commands, Application handlers that manage transactions, and Infrastructure repository/EF changes for service-line timing and metrics.

**Tech Stack:** C# on .NET 10, ASP.NET Core Minimal APIs, Mediator, EF Core 10 with Npgsql/InMemory, xUnit, Moq.

---

## File Structure

Create:

- `Domain/WorkOrders/Enums/EstimateServiceLineStatus.cs`: service-line state enum.
- `Application/WorkOrders/StartEstimateService/StartEstimateServiceCommand.cs`: start-service command.
- `Application/WorkOrders/StartEstimateService/StartEstimateServiceHandler.cs`: start-service transaction handler.
- `Application/WorkOrders/CompleteEstimateService/CompleteEstimateServiceCommand.cs`: complete-service command.
- `Application/WorkOrders/CompleteEstimateService/CompleteEstimateServiceHandler.cs`: complete-service transaction handler.
- `Application/WorkOrders/Abstractions/ICustomerApprovalEmailSender.cs`: email sender abstraction.
- `Api/WorkOrders/StartEstimateService/StartEstimateServiceEndpoint.cs`: staff start endpoint.
- `Api/WorkOrders/CompleteEstimateService/CompleteEstimateServiceEndpoint.cs`: staff complete endpoint.
- `Infrastructure/WorkOrders/Email/LoggingCustomerApprovalEmailSender.cs`: mocked email logger.
- `Infrastructure/DataAccess/Migrations/<timestamp>_EstimateServiceLineExecution.cs`: generated EF migration.

Modify:

- `Domain/WorkOrders/Entities/EstimateServiceLine.cs`: add status/timing and state methods.
- `Domain/WorkOrders/Entities/WorkOrder.cs`: add service-line start/complete aggregate methods, service-line submission invariant, completion-by-services logic.
- `Domain/WorkOrders/Entities/Estimate.cs`: expose internal service-line lookup or completion helpers only if needed by `WorkOrder`.
- `Domain/WorkOrders/Repositories/AverageServiceTimeReadModel.cs`: rename count to `CompletedServicesCount`.
- `Domain/WorkOrders/Repositories/IWorkOrderRepository.cs`: add optional `ServiceId` filter to average query signature.
- `Domain/WorkOrders/Repositories/WorkOrderServiceLineReadModel.cs`: include `Status`, `StartedAt`, `CompletedAt`.
- `Application/WorkOrders/SubmitEstimate/SubmitEstimateHandler.cs`: call mocked email sender when moving to waiting approval.
- `Application/WorkOrders/GetAverageServiceTime/*.cs`: accept optional `ServiceId`, return service-line count.
- `Application/WorkOrders/GetWorkOrderById/WorkOrderServiceLineDto.cs`: include service status and timing.
- `Application/WorkOrders/CustomerWorkOrderServiceLineDto.cs`: include service status and timing.
- `Application/WorkOrders/CustomerWorkOrderDetailsDto.cs`: map new service-line fields.
- `Api/WorkOrders/GetAverageServiceTime/*.cs`: bind optional `serviceId`, return `CompletedServicesCount`.
- `Api/WorkOrders/Responses/*WorkOrderServiceLineResponse.cs`: include service status and timing.
- `Api/WorkOrders/Responses/WorkOrderResponseMapperResponse.cs`: map new service-line fields.
- `Api/WorkOrders/WorkOrderEndpoints.cs`: register new service endpoints and remove direct complete registration.
- `Infrastructure/DataAccess/DependencyInjection.cs`: register `ICustomerApprovalEmailSender`.
- `Infrastructure/WorkOrders/Configurations/EstimateServiceLineEntityConfiguration.cs`: map new columns and indexes.
- `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`: map new read-model fields and query service-line timing.
- `Tests/Shared/WorkOrders/WorkOrderBuilder.cs`: build completed orders through service-line completion.
- `Tests/Unit/WorkOrders/WorkOrderTests.cs`: domain state-machine tests.
- `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`: handler and average query tests.
- `Tests/Integration/WorkOrders/WorkOrderRepositoryQueryTests.cs`: repository average query tests.
- `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`: API workflow and authorization tests.
- `Tests/Integration/Api/WorkOrders/Contracts/AverageServiceTimeResponse.cs`: response contract rename.
- `Tests/Integration/Api/WorkOrders/Contracts/*WorkOrderServiceLineResponse.cs`: status/timing fields.

Delete:

- `Api/WorkOrders/CompleteWorkOrder/CompleteWorkOrderEndpoint.cs`
- `Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderCommand.cs`
- `Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderHandler.cs`

---

### Task 1: Domain Service-Line State Machine

**Files:**

- Create: `Domain/WorkOrders/Enums/EstimateServiceLineStatus.cs`
- Modify: `Domain/WorkOrders/Entities/EstimateServiceLine.cs`
- Modify: `Domain/WorkOrders/Entities/WorkOrder.cs`
- Modify: `Tests/Shared/WorkOrders/WorkOrderBuilder.cs`
- Test: `Tests/Unit/WorkOrders/WorkOrderTests.cs`

- [ ] **Step 1: Write failing domain tests**

Append these tests to `Tests/Unit/WorkOrders/WorkOrderTests.cs`:

```csharp
[Fact]
public void AddServiceLine_ShouldCreatePendingServiceLine()
{
    var workOrder = new WorkOrderBuilder().BuildCreated();
    var estimate = workOrder.CreateEstimate();

    workOrder.AddServiceLine(
        estimate.Id,
        ServiceId.New(),
        Description.Create("Timing service"),
        Price.Create(80m));

    var serviceLine = Assert.Single(estimate.ServiceLines);
    Assert.Equal(EstimateServiceLineStatus.Pending, serviceLine.Status);
    Assert.Null(serviceLine.StartedAt);
    Assert.Null(serviceLine.CompletedAt);
}

[Fact]
public void StartEstimateService_ShouldMovePendingServiceToInProgress_AndStartWorkOrder()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    var beforeStart = DateTime.UtcNow;

    workOrder.StartEstimateService(estimate.Id, serviceLine.Id);

    var afterStart = DateTime.UtcNow;
    Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
    Assert.Equal(EstimateServiceLineStatus.InProgress, serviceLine.Status);
    Assert.NotNull(workOrder.StartedAt);
    Assert.NotNull(serviceLine.StartedAt);
    Assert.InRange(serviceLine.StartedAt.Value, beforeStart, afterStart);
    Assert.Equal(workOrder.StartedAt, serviceLine.StartedAt);
}

[Fact]
public void CompleteEstimateService_ShouldCompleteService_AndCompleteWorkOrder_WhenAllServicesAreCompleted()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
    var beforeComplete = DateTime.UtcNow;

    workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);

    var afterComplete = DateTime.UtcNow;
    Assert.Equal(EstimateServiceLineStatus.Completed, serviceLine.Status);
    Assert.NotNull(serviceLine.CompletedAt);
    Assert.InRange(serviceLine.CompletedAt.Value, beforeComplete, afterComplete);
    Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
    Assert.Equal(serviceLine.CompletedAt, workOrder.CompletedAt);
}

[Fact]
public void CompleteEstimateService_ShouldKeepWorkOrderInProgress_WhenAnotherServiceIsNotCompleted()
{
    var workOrder = new WorkOrderBuilder().BuildCreated();
    var estimate = workOrder.CreateEstimate();
    workOrder.AddServiceLine(estimate.Id, ServiceId.New(), Description.Create("First service"), Price.Create(100m));
    workOrder.AddServiceLine(estimate.Id, ServiceId.New(), Description.Create("Second service"), Price.Create(150m));
    workOrder.SubmitEstimate(estimate.Id);
    workOrder.ApproveEstimate(estimate.Id);
    var firstService = estimate.ServiceLines.First();

    workOrder.StartEstimateService(estimate.Id, firstService.Id);
    workOrder.CompleteEstimateService(estimate.Id, firstService.Id);

    Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
    Assert.Equal(EstimateServiceLineStatus.Completed, firstService.Status);
    Assert.Contains(estimate.ServiceLines, line => line.Status == EstimateServiceLineStatus.Pending);
    Assert.Null(workOrder.CompletedAt);
}

[Fact]
public void CompleteEstimateService_ShouldThrowBusinessRuleViolationException_WhenServiceIsPending()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();

    var exception = Assert.Throws<BusinessRuleViolationException>(
        () => workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id));

    Assert.Equal("Work order must be in progress before completing services.", exception.Message);
    Assert.Equal(EstimateServiceLineStatus.Pending, serviceLine.Status);
    Assert.Null(serviceLine.CompletedAt);
}

[Fact]
public void StartEstimateService_ShouldThrowBusinessRuleViolationException_WhenServiceIsCompleted()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
    workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);

    var exception = Assert.Throws<BusinessRuleViolationException>(
        () => workOrder.StartEstimateService(estimate.Id, serviceLine.Id));

    Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
    Assert.Equal(EstimateServiceLineStatus.Completed, serviceLine.Status);
}

[Fact]
public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateHasOnlyInventory()
{
    var workOrder = new WorkOrderBuilder().BuildCreated();
    var estimate = workOrder.CreateEstimate();
    WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);

    var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

    Assert.Equal("Estimate must contain at least one service line before submission.", exception.Message);
    Assert.Equal(WorkOrderStatus.Created, workOrder.Status);
    Assert.Equal(EstimateStatus.Draft, estimate.Status);
}
```

Add this using to `Tests/Unit/WorkOrders/WorkOrderTests.cs`:

```csharp
using GarageFlow.Domain.WorkOrders.Enums;
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderTests"
```

Expected: compile fails because `EstimateServiceLineStatus`, `EstimateServiceLine.Status`, `EstimateServiceLine.StartedAt`, `EstimateServiceLine.CompletedAt`, `WorkOrder.StartEstimateService`, and `WorkOrder.CompleteEstimateService` do not exist.

- [ ] **Step 3: Add the service-line status enum**

Create `Domain/WorkOrders/Enums/EstimateServiceLineStatus.cs`:

```csharp
namespace GarageFlow.Domain.WorkOrders.Enums;

public enum EstimateServiceLineStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3
}
```

- [ ] **Step 4: Add service-line state and timing behavior**

Modify `Domain/WorkOrders/Entities/EstimateServiceLine.cs`.

Add this using:

```csharp
using GarageFlow.Domain.WorkOrders.Enums;
```

Add these properties after `UnitPrice`:

```csharp
public EstimateServiceLineStatus Status { get; private set; }
public DateTime? StartedAt { get; private set; }
public DateTime? CompletedAt { get; private set; }
```

Set `Status` in the constructor:

```csharp
Status = EstimateServiceLineStatus.Pending;
```

Add these internal methods before validation helpers:

```csharp
internal void Start()
{
    var startedAt = DateTime.UtcNow;
    TransitionTo(EstimateServiceLineStatus.InProgress, startedAt);
    StartedAt = startedAt;
}

internal void Complete()
{
    var completedAt = DateTime.UtcNow;
    TransitionTo(EstimateServiceLineStatus.Completed, completedAt);
    CompletedAt = completedAt;
}

private void TransitionTo(EstimateServiceLineStatus newStatus, DateTime occurredAt)
{
    if (Status == newStatus)
    {
        return;
    }

    var isAllowed = (Status, newStatus) switch
    {
        (EstimateServiceLineStatus.Pending, EstimateServiceLineStatus.InProgress) => true,
        (EstimateServiceLineStatus.InProgress, EstimateServiceLineStatus.Completed) => true,
        _ => false
    };

    if (!isAllowed)
    {
        throw new BusinessRuleViolationException($"Estimate service line status transition from '{Status}' to '{newStatus}' is not allowed.");
    }

    Status = newStatus;
    UpdatedAt = occurredAt;
}
```

- [ ] **Step 5: Add WorkOrder aggregate service methods and submission invariant**

Modify `Domain/WorkOrders/Entities/WorkOrder.cs`.

Replace `EnsureEstimateCanBeSubmitted` with:

```csharp
private static void EnsureEstimateCanBeSubmitted(Estimate estimate)
{
    if (estimate.Status != EstimateStatus.Draft)
    {
        throw new BusinessRuleViolationException("Only draft estimates can be edited.");
    }

    if (estimate.ServiceLines.Count == 0)
    {
        throw new BusinessRuleViolationException("Estimate must contain at least one service line before submission.");
    }
}
```

Add these public methods after `StartWork()`:

```csharp
public void StartEstimateService(EstimateId estimateId, EstimateServiceLineId lineId)
{
    EnsureNotFinalizedForContentChanges();

    var estimate = GetApprovedEstimateOrThrow(estimateId);
    var serviceLine = GetEstimateServiceLineOrThrow(estimate, lineId);

    if (Status == WorkOrderStatus.Approved)
    {
        StartWork();
    }
    else if (Status != WorkOrderStatus.InProgress)
    {
        throw new BusinessRuleViolationException("Work order must be approved before starting services.");
    }

    var startedAtBeforeLineStart = StartedAt;
    serviceLine.Start();

    if (startedAtBeforeLineStart is null && StartedAt is null)
    {
        StartedAt = serviceLine.StartedAt;
    }

    UpdatedAt = DateTime.UtcNow;
}

public void CompleteEstimateService(EstimateId estimateId, EstimateServiceLineId lineId)
{
    if (Status != WorkOrderStatus.InProgress)
    {
        throw new BusinessRuleViolationException("Work order must be in progress before completing services.");
    }

    var estimate = GetApprovedEstimateOrThrow(estimateId);
    var serviceLine = GetEstimateServiceLineOrThrow(estimate, lineId);

    serviceLine.Complete();

    if (estimate.ServiceLines.All(line => line.Status == EstimateServiceLineStatus.Completed))
    {
        CompleteFromServiceLines(serviceLine.CompletedAt);
    }

    UpdatedAt = DateTime.UtcNow;
}
```

Change the existing public `Complete()` method to this private helper:

```csharp
private void CompleteFromServiceLines(DateTime? completedAt)
{
    var approvedEstimatesCount = _estimates.Count(estimate => estimate.Status == EstimateStatus.Approved);
    if (approvedEstimatesCount != 1)
    {
        throw new BusinessRuleViolationException("Work order requires exactly one approved estimate before completion.");
    }

    if (Status == WorkOrderStatus.Completed)
    {
        return;
    }

    var occurredAt = completedAt ?? DateTime.UtcNow;
    TransitionTo(WorkOrderStatus.Completed, occurredAt);
    CompletedAt = occurredAt;
}
```

Add these private helpers near `GetEditableEstimateOrThrow`:

```csharp
private Estimate GetApprovedEstimateOrThrow(EstimateId estimateId)
{
    var estimate = GetEstimateOrThrow(estimateId);
    if (estimate.Status != EstimateStatus.Approved)
    {
        throw new BusinessRuleViolationException("Only approved estimates can have services executed.");
    }

    return estimate;
}

private static EstimateServiceLine GetEstimateServiceLineOrThrow(Estimate estimate, EstimateServiceLineId lineId)
{
    var serviceLine = estimate.ServiceLines.SingleOrDefault(candidate => candidate.Id == lineId);
    if (serviceLine is null)
    {
        throw new NotFoundException($"Estimate service line with ID '{lineId.Value}' was not found.");
    }

    return serviceLine;
}
```

Add this transition to `TransitionTo`:

```csharp
(WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => true,
```

Keep the existing `StartWork()` public method for the existing start-work endpoint. It remains a valid way to set the work order to `InProgress`, while service-line `StartedAt` is set only by `StartEstimateService`.

- [ ] **Step 6: Update the shared WorkOrderBuilder**

Modify `Tests/Shared/WorkOrders/WorkOrderBuilder.cs`.

Replace `BuildCompleted()` with:

```csharp
public WorkOrder BuildCompleted()
{
    var workOrder = BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
    workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);
    return workOrder;
}
```

Add helper:

```csharp
public static void AddDefaultServiceLine(WorkOrder workOrder, EstimateId estimateId)
{
    workOrder.AddServiceLine(
        estimateId,
        ServiceId.New(),
        Description.Create("Diagnosis labor"),
        Price.Create(120.00m));
}
```

- [ ] **Step 7: Run domain tests to verify GREEN**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderTests"
```

Expected: tests compile and the new domain tests pass. Existing tests that call `workOrder.Complete()` fail until Task 2 removes or rewrites those direct-completion assertions.

- [ ] **Step 8: Commit Task 1**

Run:

```bash
git add Domain/WorkOrders Tests/Shared/WorkOrders/WorkOrderBuilder.cs Tests/Unit/WorkOrders/WorkOrderTests.cs
git commit -m "feat: add estimate service line state machine"
```

---

### Task 2: Remove Direct Completion From Tests And Application Surface

**Files:**

- Delete: `Api/WorkOrders/CompleteWorkOrder/CompleteWorkOrderEndpoint.cs`
- Delete: `Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderCommand.cs`
- Delete: `Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderHandler.cs`
- Modify: `Api/WorkOrders/WorkOrderEndpoints.cs`
- Modify: `Tests/Unit/WorkOrders/WorkOrderTests.cs`
- Modify: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`
- Test: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`

- [ ] **Step 1: Write failing API test for removed direct completion route**

Add this test to `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`:

```csharp
[Fact]
public async Task CompleteWorkOrder_ShouldReturn404_WhenDirectCompletionRouteIsRemoved()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var staffClient = await app.CreateStaffClientAsync();
    var workOrder = await CreateWorkOrderAsync(staffClient);

    var response = await staffClient.PostAsync($"/work-orders/{workOrder.Id}/complete", content: null);

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.NotFound);
}
```

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "CompleteWorkOrder_ShouldReturn404_WhenDirectCompletionRouteIsRemoved"
```

Expected: FAIL because the current route still exists and returns a non-404 status.

- [ ] **Step 3: Remove endpoint registration and delete direct completion slices**

Modify `Api/WorkOrders/WorkOrderEndpoints.cs`.

Remove this using:

```csharp
using GarageFlow.Api.WorkOrders.CompleteWorkOrder;
```

Remove this registration:

```csharp
staffRoutes.MapCompleteWorkOrderEndpoint();
```

Delete:

```text
Api/WorkOrders/CompleteWorkOrder/CompleteWorkOrderEndpoint.cs
Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderCommand.cs
Application/WorkOrders/CompleteWorkOrder/CompleteWorkOrderHandler.cs
```

- [ ] **Step 4: Rewrite unit tests that directly call WorkOrder.Complete**

In `Tests/Unit/WorkOrders/WorkOrderTests.cs`, delete or rewrite direct `Complete()` tests.

Replace the old completion success test with:

```csharp
[Fact]
public void CompleteEstimateService_ShouldSetWorkOrderCompletedAt_WhenLastServiceCompletes()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    workOrder.StartEstimateService(estimate.Id, serviceLine.Id);

    workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);

    Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
    Assert.NotNull(workOrder.CompletedAt);
    Assert.True(workOrder.CompletedAt.Value >= workOrder.StartedAt!.Value);
}
```

Replace idempotent direct completion tests with service-line transition tests:

```csharp
[Fact]
public void CompleteEstimateService_CalledTwice_ShouldThrowBusinessRuleViolationException()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
    workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);

    var exception = Assert.Throws<BusinessRuleViolationException>(
        () => workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id));

    Assert.Equal("Work order must be in progress before completing services.", exception.Message);
}
```

Replace builder flows that call `workOrder.Complete()` with `new WorkOrderBuilder().BuildCompleted()`.

In `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`, remove tests for `CompleteWorkOrderHandler` and remove this using:

```csharp
using GarageFlow.Application.WorkOrders.CompleteWorkOrder;
```

- [ ] **Step 5: Run focused tests to verify GREEN**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "CompleteWorkOrder_ShouldReturn404_WhenDirectCompletionRouteIsRemoved"
```

Expected: all focused tests pass.

- [ ] **Step 6: Commit Task 2**

Run:

```bash
git add Api/WorkOrders Application/WorkOrders Tests/Unit/WorkOrders Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs
git add -u Api/WorkOrders/CompleteWorkOrder Application/WorkOrders/CompleteWorkOrder
git commit -m "feat: remove direct work order completion"
```

---

### Task 3: Application Handlers And Mocked Email Sender

**Files:**

- Create: `Application/WorkOrders/StartEstimateService/StartEstimateServiceCommand.cs`
- Create: `Application/WorkOrders/StartEstimateService/StartEstimateServiceHandler.cs`
- Create: `Application/WorkOrders/CompleteEstimateService/CompleteEstimateServiceCommand.cs`
- Create: `Application/WorkOrders/CompleteEstimateService/CompleteEstimateServiceHandler.cs`
- Create: `Application/WorkOrders/Abstractions/ICustomerApprovalEmailSender.cs`
- Create: `Infrastructure/WorkOrders/Email/LoggingCustomerApprovalEmailSender.cs`
- Modify: `Application/WorkOrders/SubmitEstimate/SubmitEstimateHandler.cs`
- Modify: `Infrastructure/DataAccess/DependencyInjection.cs`
- Test: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`

- [ ] **Step 1: Write failing handler tests**

Add these tests to `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`:

```csharp
[Fact]
public async Task StartEstimateService_ShouldStartServiceLine_AndCommit()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
    var unitOfWorkMock = CreateUnitOfWorkMock();
    var handler = new StartEstimateServiceHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

    await handler.Handle(
        new StartEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, serviceLine.Id.Value),
        CancellationToken.None);

    Assert.Equal(EstimateServiceLineStatus.InProgress, serviceLine.Status);
    Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
    workOrderRepositoryMock.Verify(
        x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
        Times.Once);
    unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task CompleteEstimateService_ShouldCompleteServiceLine_AndCommit()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var estimate = workOrder.Estimates.Single();
    var serviceLine = estimate.ServiceLines.Single();
    workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
    var unitOfWorkMock = CreateUnitOfWorkMock();
    var handler = new CompleteEstimateServiceHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

    await handler.Handle(
        new CompleteEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, serviceLine.Id.Value),
        CancellationToken.None);

    Assert.Equal(EstimateServiceLineStatus.Completed, serviceLine.Status);
    Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
    unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task StartEstimateService_ShouldRollback_WhenWorkOrderIsMissing()
{
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
    var unitOfWorkMock = CreateUnitOfWorkMock();
    var handler = new StartEstimateServiceHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);
    var workOrderId = Guid.NewGuid();

    var exception = await Assert.ThrowsAsync<NotFoundException>(
        async () => await handler.Handle(
            new StartEstimateServiceCommand(workOrderId, Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None));

    Assert.Equal($"Work order with ID '{workOrderId}' was not found.", exception.Message);
    unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task SubmitEstimate_ShouldSendApprovalEmail_WhenWorkOrderMovesToWaitingApproval()
{
    var workOrder = new WorkOrderBuilder().BuildCreated();
    var estimate = workOrder.CreateEstimate();
    WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
    var unitOfWorkMock = CreateUnitOfWorkMock();
    var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();
    emailSenderMock
        .Setup(x => x.SendEstimateWaitingApprovalAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    var handler = new SubmitEstimateHandler(
        workOrderRepositoryMock.Object,
        unitOfWorkMock.Object,
        emailSenderMock.Object);

    await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value), CancellationToken.None);

    emailSenderMock.Verify(
        x => x.SendEstimateWaitingApprovalAsync(
            workOrder.Id.Value,
            estimate.Id.Value,
            workOrder.CustomerId.Value,
            It.IsAny<CancellationToken>()),
        Times.Once);
    unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

Add these usings:

```csharp
using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.Application.WorkOrders.CompleteEstimateService;
using GarageFlow.Application.WorkOrders.StartEstimateService;
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "StartEstimateService_ShouldStartServiceLine_AndCommit|CompleteEstimateService_ShouldCompleteServiceLine_AndCommit|SubmitEstimate_ShouldSendApprovalEmail_WhenWorkOrderMovesToWaitingApproval"
```

Expected: compile fails because new handlers, commands, and email abstraction do not exist.

- [ ] **Step 3: Add Application email abstraction**

Create `Application/WorkOrders/Abstractions/ICustomerApprovalEmailSender.cs`:

```csharp
namespace GarageFlow.Application.WorkOrders.Abstractions;

public interface ICustomerApprovalEmailSender
{
    Task SendEstimateWaitingApprovalAsync(
        Guid workOrderId,
        Guid estimateId,
        Guid customerId,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Add start service command and handler**

Create `Application/WorkOrders/StartEstimateService/StartEstimateServiceCommand.cs`:

```csharp
using Mediator;

namespace GarageFlow.Application.WorkOrders.StartEstimateService;

public sealed record StartEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : IRequest<Unit>;
```

Create `Application/WorkOrders/StartEstimateService/StartEstimateServiceHandler.cs`:

```csharp
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.StartEstimateService;

public sealed class StartEstimateServiceHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<StartEstimateServiceCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<Unit> Handle(StartEstimateServiceCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var lineId = EstimateServiceLineId.From(request.LineId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
            if (workOrder is null)
            {
                throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
            }

            workOrder.StartEstimateService(estimateId, lineId);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Unit.Value;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
```

- [ ] **Step 5: Add complete service command and handler**

Create `Application/WorkOrders/CompleteEstimateService/CompleteEstimateServiceCommand.cs`:

```csharp
using Mediator;

namespace GarageFlow.Application.WorkOrders.CompleteEstimateService;

public sealed record CompleteEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : IRequest<Unit>;
```

Create `Application/WorkOrders/CompleteEstimateService/CompleteEstimateServiceHandler.cs`:

```csharp
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.CompleteEstimateService;

public sealed class CompleteEstimateServiceHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteEstimateServiceCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<Unit> Handle(CompleteEstimateServiceCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var lineId = EstimateServiceLineId.From(request.LineId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
            if (workOrder is null)
            {
                throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
            }

            workOrder.CompleteEstimateService(estimateId, lineId);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Unit.Value;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
```

- [ ] **Step 6: Update SubmitEstimateHandler to send mocked email**

Modify `Application/WorkOrders/SubmitEstimate/SubmitEstimateHandler.cs`.

Add usings:

```csharp
using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.Domain.WorkOrders.Enums;
```

Change constructor parameters to:

```csharp
public sealed class SubmitEstimateHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork,
    ICustomerApprovalEmailSender emailSender) : IRequestHandler<SubmitEstimateCommand, Unit>
```

Add field:

```csharp
private readonly ICustomerApprovalEmailSender _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
```

Inside the try block, before `workOrder.SubmitEstimate(estimateId);`, add:

```csharp
var previousStatus = workOrder.Status;
```

After `workOrder.SubmitEstimate(estimateId);`, add:

```csharp
if (previousStatus != WorkOrderStatus.WaitingApproval && workOrder.Status == WorkOrderStatus.WaitingApproval)
{
    await _emailSender.SendEstimateWaitingApprovalAsync(
        workOrder.Id.Value,
        estimateId.Value,
        workOrder.CustomerId.Value,
        cancellationToken);
}
```

- [ ] **Step 7: Add logging email sender and DI registration**

Create `Infrastructure/WorkOrders/Email/LoggingCustomerApprovalEmailSender.cs`:

```csharp
using GarageFlow.Application.WorkOrders.Abstractions;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Infrastructure.WorkOrders.Email;

public sealed class LoggingCustomerApprovalEmailSender(
    ILogger<LoggingCustomerApprovalEmailSender> logger) : ICustomerApprovalEmailSender
{
    private readonly ILogger<LoggingCustomerApprovalEmailSender> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task SendEstimateWaitingApprovalAsync(
        Guid workOrderId,
        Guid estimateId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Approval email sent to customer {CustomerId} for work order {WorkOrderId} and estimate {EstimateId}.",
            customerId,
            workOrderId,
            estimateId);

        return Task.CompletedTask;
    }
}
```

Modify `Infrastructure/DataAccess/DependencyInjection.cs`.

Add using:

```csharp
using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.Infrastructure.WorkOrders.Email;
```

Add registration before repository registrations or near work-order registration:

```csharp
builder.Services.AddScoped<ICustomerApprovalEmailSender, LoggingCustomerApprovalEmailSender>();
```

- [ ] **Step 8: Run handler tests to verify GREEN**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderHandlersTests"
```

Expected: handler tests pass after updating any existing `SubmitEstimateHandler` constructor calls in the same file to pass a mocked `ICustomerApprovalEmailSender`.

- [ ] **Step 9: Commit Task 3**

Run:

```bash
git add Application/WorkOrders Infrastructure/WorkOrders/Email Infrastructure/DataAccess/DependencyInjection.cs Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs
git commit -m "feat: add service execution handlers and approval email log"
```

---

### Task 4: Staff API Endpoints For Service Start And Complete

**Files:**

- Create: `Api/WorkOrders/StartEstimateService/StartEstimateServiceEndpoint.cs`
- Create: `Api/WorkOrders/CompleteEstimateService/CompleteEstimateServiceEndpoint.cs`
- Modify: `Api/WorkOrders/WorkOrderEndpoints.cs`
- Test: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`

- [ ] **Step 1: Write failing endpoint tests for authorization**

Add these tests to `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`:

```csharp
[Fact]
public async Task StartEstimateService_ShouldReturn401_WhenRequestHasNoToken()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var client = app.CreateClient();

    var response = await client.PostAsync(
        $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/start",
        content: null);

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
}

[Fact]
public async Task CompleteEstimateService_ShouldReturn401_WhenRequestHasNoToken()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var client = app.CreateClient();

    var response = await client.PostAsync(
        $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/complete",
        content: null);

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
}
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "StartEstimateService_ShouldReturn401_WhenRequestHasNoToken|CompleteEstimateService_ShouldReturn401_WhenRequestHasNoToken"
```

Expected: FAIL with `404 NotFound` because the routes do not exist.

- [ ] **Step 3: Add start endpoint**

Create `Api/WorkOrders/StartEstimateService/StartEstimateServiceEndpoint.cs`:

```csharp
using GarageFlow.Application.WorkOrders.StartEstimateService;
using Mediator;

namespace GarageFlow.Api.WorkOrders.StartEstimateService;

public static class StartEstimateServiceEndpoint
{
    public static IEndpointRouteBuilder MapStartEstimateServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates/{estimateId:guid}/services/{lineId:guid}/start", StartEstimateService)
            .WithName("StartEstimateService")
            .WithTags("Work Orders")
            .WithSummary("Start an approved estimate service line")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> StartEstimateService(
        Guid id,
        Guid estimateId,
        Guid lineId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new StartEstimateServiceCommand(id, estimateId, lineId), cancellationToken);
        return Results.NoContent();
    }
}
```

- [ ] **Step 4: Add complete endpoint**

Create `Api/WorkOrders/CompleteEstimateService/CompleteEstimateServiceEndpoint.cs`:

```csharp
using GarageFlow.Application.WorkOrders.CompleteEstimateService;
using Mediator;

namespace GarageFlow.Api.WorkOrders.CompleteEstimateService;

public static class CompleteEstimateServiceEndpoint
{
    public static IEndpointRouteBuilder MapCompleteEstimateServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates/{estimateId:guid}/services/{lineId:guid}/complete", CompleteEstimateService)
            .WithName("CompleteEstimateService")
            .WithTags("Work Orders")
            .WithSummary("Complete an in-progress estimate service line")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CompleteEstimateService(
        Guid id,
        Guid estimateId,
        Guid lineId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteEstimateServiceCommand(id, estimateId, lineId), cancellationToken);
        return Results.NoContent();
    }
}
```

- [ ] **Step 5: Register endpoints**

Modify `Api/WorkOrders/WorkOrderEndpoints.cs`.

Add usings:

```csharp
using GarageFlow.Api.WorkOrders.CompleteEstimateService;
using GarageFlow.Api.WorkOrders.StartEstimateService;
```

Add registrations under staff routes after `MapStartWorkEndpoint()`:

```csharp
staffRoutes.MapStartEstimateServiceEndpoint();
staffRoutes.MapCompleteEstimateServiceEndpoint();
```

- [ ] **Step 6: Run endpoint authorization tests to verify GREEN**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "StartEstimateService_ShouldReturn401_WhenRequestHasNoToken|CompleteEstimateService_ShouldReturn401_WhenRequestHasNoToken"
```

Expected: both tests pass with `401 Unauthorized`.

- [ ] **Step 7: Commit Task 4**

Run:

```bash
git add Api/WorkOrders Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs
git commit -m "feat: add service execution API endpoints"
```

---

### Task 5: Persist Service-Line Status And Timing, Update Details Read Models

**Files:**

- Modify: `Domain/WorkOrders/Repositories/WorkOrderServiceLineReadModel.cs`
- Modify: `Application/WorkOrders/GetWorkOrderById/WorkOrderServiceLineDto.cs`
- Modify: `Application/WorkOrders/CustomerWorkOrderServiceLineDto.cs`
- Modify: `Application/WorkOrders/CustomerWorkOrderDetailsDto.cs`
- Modify: `Api/WorkOrders/Responses/WorkOrderServiceLineResponse.cs`
- Modify: `Api/WorkOrders/Responses/CustomerWorkOrderServiceLineResponse.cs`
- Modify: `Api/WorkOrders/Responses/WorkOrderResponseMapperResponse.cs`
- Modify: `Infrastructure/WorkOrders/Configurations/EstimateServiceLineEntityConfiguration.cs`
- Modify: `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`
- Create: `Infrastructure/DataAccess/Migrations/<timestamp>_EstimateServiceLineExecution.cs`
- Test: `Tests/Integration/Api/WorkOrders/Contracts/WorkOrderServiceLineResponse.cs`
- Test: `Tests/Integration/Api/WorkOrders/Contracts/CustomerWorkOrderServiceLineResponse.cs`

- [ ] **Step 1: Write failing API detail assertion**

In `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`, add a service execution workflow test:

```csharp
[Fact]
public async Task Staff_ShouldStartAndCompleteEstimateService_AndSeeServiceExecutionDetails()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var staffClient = await app.CreateStaffClientAsync();
    var workOrder = await CreateWorkOrderAsync(staffClient);
    var estimate = await CreateEstimateAsync(staffClient, workOrder.Id);
    var service = await CreateServiceAsync(
        staffClient,
        new ServiceBuilder()
            .WithDescription($"Execution service {Guid.NewGuid():N}")
            .WithPrice(125m));
    var addServiceResponse = await staffClient.PostAsJsonAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services",
        new AddEstimateServiceRequest(service.Id));
    HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);
    var addServicePayload = await HttpResponseAssertions.ReadRequiredJsonAsync<AddEstimateServiceResponse>(addServiceResponse);
    await SubmitAndApproveEstimateAsync(app, staffClient, workOrder.Id, estimate.Id);

    var startResponse = await staffClient.PostAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{addServicePayload.Id}/start",
        content: null);
    HttpResponseAssertions.AssertStatus(startResponse, HttpStatusCode.NoContent);

    var inProgressDetails = await GetWorkOrderDetailsAsync(staffClient, workOrder.Id);
    var inProgressService = Assert.Single(inProgressDetails.Estimates.Single().ServiceLines);
    Assert.Equal("InProgress", inProgressService.Status);
    Assert.NotNull(inProgressService.StartedAt);
    Assert.Null(inProgressService.CompletedAt);
    Assert.Equal("InProgress", inProgressDetails.Status);

    var completeResponse = await staffClient.PostAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{addServicePayload.Id}/complete",
        content: null);
    HttpResponseAssertions.AssertStatus(completeResponse, HttpStatusCode.NoContent);

    var completedDetails = await GetWorkOrderDetailsAsync(staffClient, workOrder.Id);
    var completedService = Assert.Single(completedDetails.Estimates.Single().ServiceLines);
    Assert.Equal("Completed", completedService.Status);
    Assert.NotNull(completedService.StartedAt);
    Assert.NotNull(completedService.CompletedAt);
    Assert.Equal("Completed", completedDetails.Status);
}
```

Add helper if missing:

```csharp
private static async Task<WorkOrderDetailsResponse> GetWorkOrderDetailsAsync(HttpClient client, Guid workOrderId)
{
    var response = await client.GetAsync($"/work-orders/{workOrderId}");
    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    return await HttpResponseAssertions.ReadRequiredJsonAsync<WorkOrderDetailsResponse>(response);
}
```

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "Staff_ShouldStartAndCompleteEstimateService_AndSeeServiceExecutionDetails"
```

Expected: compile fails because test contracts lack `Status`, `StartedAt`, and `CompletedAt`, or runtime fails because EF has no mapped columns.

- [ ] **Step 3: Update read model, DTOs, and API contracts**

Modify `Domain/WorkOrders/Repositories/WorkOrderServiceLineReadModel.cs`:

```csharp
namespace GarageFlow.Domain.WorkOrders.Repositories;

public sealed record WorkOrderServiceLineReadModel(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string DescriptionSnapshot,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);
```

Modify `Application/WorkOrders/GetWorkOrderById/WorkOrderServiceLineDto.cs` and `Application/WorkOrders/CustomerWorkOrderServiceLineDto.cs` to include the same final three fields:

```csharp
string Status,
DateTime? StartedAt,
DateTime? CompletedAt
```

Modify `Api/WorkOrders/Responses/WorkOrderServiceLineResponse.cs` and `Api/WorkOrders/Responses/CustomerWorkOrderServiceLineResponse.cs` to include:

```csharp
string Status,
DateTime? StartedAt,
DateTime? CompletedAt
```

Modify `Tests/Integration/Api/WorkOrders/Contracts/WorkOrderServiceLineResponse.cs` and `Tests/Integration/Api/WorkOrders/Contracts/CustomerWorkOrderServiceLineResponse.cs` to include:

```csharp
public string Status { get; init; } = string.Empty;
public DateTime? StartedAt { get; init; }
public DateTime? CompletedAt { get; init; }
```

- [ ] **Step 4: Update mappers**

Modify `Application/WorkOrders/CustomerWorkOrderDetailsDto.cs`.

Update `MapServiceLine`:

```csharp
private static WorkOrderServiceLineDto MapServiceLine(WorkOrderServiceLineReadModel line)
{
    return new WorkOrderServiceLineDto(
        line.Id,
        line.EstimateId,
        line.ServiceId,
        line.DescriptionSnapshot,
        line.UnitPrice,
        line.TotalPrice,
        line.Status,
        line.StartedAt,
        line.CompletedAt);
}
```

Update `MapCustomerServiceLine`:

```csharp
private static CustomerWorkOrderServiceLineDto MapCustomerServiceLine(WorkOrderServiceLineReadModel line)
{
    return new CustomerWorkOrderServiceLineDto(
        line.Id,
        line.EstimateId,
        line.ServiceId,
        line.DescriptionSnapshot,
        line.UnitPrice,
        line.TotalPrice,
        line.Status,
        line.StartedAt,
        line.CompletedAt);
}
```

Modify `Api/WorkOrders/Responses/WorkOrderResponseMapperResponse.cs`.

Update both service-line mapping methods to pass:

```csharp
line.Status,
line.StartedAt,
line.CompletedAt
```

- [ ] **Step 5: Update repository detail mapping**

Modify service-line projection in `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`:

```csharp
var serviceLines = estimate.ServiceLines
    .OrderBy(line => line.CreatedAt)
    .ThenBy(line => line.Id.Value)
    .Select(line => new WorkOrderServiceLineReadModel(
        line.Id.Value,
        line.EstimateId.Value,
        line.ServiceId.Value,
        line.DescriptionSnapshot.Value,
        line.UnitPrice.Value,
        line.TotalPrice.Value,
        line.Status.ToString(),
        line.StartedAt,
        line.CompletedAt))
    .ToList();
```

- [ ] **Step 6: Update EF configuration**

Modify `Infrastructure/WorkOrders/Configurations/EstimateServiceLineEntityConfiguration.cs`.

Add after `UnitPrice` mapping:

```csharp
builder.Property(line => line.Status)
    .HasConversion<string>()
    .HasMaxLength(32)
    .IsRequired();

builder.Property(line => line.StartedAt);

builder.Property(line => line.CompletedAt);
```

Add indexes near existing indexes:

```csharp
builder.HasIndex(line => line.ServiceId);
builder.HasIndex(line => line.CompletedAt);
builder.HasIndex(line => new { line.ServiceId, line.CompletedAt });
```

- [ ] **Step 7: Generate migration**

Run:

```bash
dotnet ef migrations add EstimateServiceLineExecution --project Infrastructure --startup-project Api --context GarageFlowDbContext --output-dir DataAccess/Migrations
```

Expected: EF creates a migration adding `Status`, `StartedAt`, and `CompletedAt` to `WorkOrderEstimateServiceLines`, plus indexes for `ServiceId`, `CompletedAt`, and `(ServiceId, CompletedAt)`.

Open the generated migration and confirm `Status` has default value `"Pending"` for existing rows:

```csharp
defaultValue: "Pending"
```

If EF does not generate that default, edit the generated migration `AddColumn<string>` call for `Status` to include:

```csharp
defaultValue: "Pending"
```

- [ ] **Step 8: Run integration workflow test to verify GREEN**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "Staff_ShouldStartAndCompleteEstimateService_AndSeeServiceExecutionDetails"
```

Expected: test passes and detail payload includes service status/timestamps.

- [ ] **Step 9: Commit Task 5**

Run:

```bash
git add Domain/WorkOrders/Repositories Application/WorkOrders Api/WorkOrders Infrastructure/WorkOrders Infrastructure/DataAccess/Migrations Tests/Integration
git commit -m "feat: persist service line execution details"
```

---

### Task 6: Average Service Time From Completed Service Lines

**Files:**

- Modify: `Domain/WorkOrders/Repositories/AverageServiceTimeReadModel.cs`
- Modify: `Domain/WorkOrders/Repositories/IWorkOrderRepository.cs`
- Modify: `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeQuery.cs`
- Modify: `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResult.cs`
- Modify: `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeHandler.cs`
- Modify: `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeEndpoint.cs`
- Modify: `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResponse.cs`
- Modify: `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`
- Modify: `Tests/Integration/Api/WorkOrders/Contracts/AverageServiceTimeResponse.cs`
- Test: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`
- Test: `Tests/Integration/WorkOrders/WorkOrderRepositoryQueryTests.cs`
- Test: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`

- [ ] **Step 1: Write failing unit tests for optional serviceId**

Update average service time tests in `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`.

Replace `GetAverageServiceTime_ShouldReturnAverageDuration_WhenWindowHasCompletedWorkOrders` with:

```csharp
[Fact]
public async Task GetAverageServiceTime_ShouldReturnAverageDuration_WhenWindowHasCompletedServices()
{
    var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    var to = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
    var serviceId = Guid.NewGuid();
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(
        averageServiceTime: new AverageServiceTimeReadModel(
            CompletedServicesCount: 3,
            AverageDurationMinutes: 184.5d));
    var handler = new GetAverageServiceTimeHandler(workOrderRepositoryMock.Object);

    var result = await handler.Handle(new GetAverageServiceTimeQuery(from, to, serviceId), CancellationToken.None);

    Assert.Equal(from, result.From);
    Assert.Equal(to, result.To);
    Assert.Equal(serviceId, result.ServiceId);
    Assert.Equal(3, result.CompletedServicesCount);
    Assert.Equal(184.5d, result.AverageDurationMinutes);
    workOrderRepositoryMock.Verify(
        x => x.GetAverageServiceTimeAsync(from, to, It.Is<ServiceId?>(id => id!.Value == serviceId), It.IsAny<CancellationToken>()),
        Times.Once);
}
```

Replace no-data test assertions with:

```csharp
Assert.Equal(0, result.CompletedServicesCount);
Assert.Null(result.AverageDurationMinutes);
```

Update repository mock setup to expect the new signature:

```csharp
repositoryMock
    .Setup(x => x.GetAverageServiceTimeAsync(
        It.IsAny<DateTime>(),
        It.IsAny<DateTime>(),
        It.IsAny<ServiceId?>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(averageServiceTime ?? new AverageServiceTimeReadModel(
        CompletedServicesCount: 0,
        AverageDurationMinutes: null));
```

- [ ] **Step 2: Run unit tests to verify RED**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "GetAverageServiceTime"
```

Expected: compile fails because query/result/read model still use work-order count and no `ServiceId`.

- [ ] **Step 3: Update Domain repository contracts**

Modify `Domain/WorkOrders/Repositories/AverageServiceTimeReadModel.cs`:

```csharp
namespace GarageFlow.Domain.WorkOrders.Repositories;

public sealed record AverageServiceTimeReadModel(
    int CompletedServicesCount,
    double? AverageDurationMinutes);
```

Modify `Domain/WorkOrders/Repositories/IWorkOrderRepository.cs`.

Add using:

```csharp
using GarageFlow.Domain.Services.ValueObjects;
```

Change method signature:

```csharp
Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
    DateTime completedFrom,
    DateTime completedTo,
    ServiceId? serviceId = null,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Update Application query, result, and handler**

Modify `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeQuery.cs`:

```csharp
using Mediator;

namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeQuery(DateTime From, DateTime To, Guid? ServiceId = null)
    : IRequest<GetAverageServiceTimeResult>;
```

Modify `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResult.cs`:

```csharp
namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResult(
    DateTime From,
    DateTime To,
    Guid? ServiceId,
    int CompletedServicesCount,
    double? AverageDurationMinutes);
```

Modify handler:

```csharp
using GarageFlow.Domain.Services.ValueObjects;
```

Use:

```csharp
var serviceId = request.ServiceId.HasValue ? ServiceId.From(request.ServiceId.Value) : (ServiceId?)null;
var averageServiceTime = await _workOrderRepository.GetAverageServiceTimeAsync(
    request.From,
    request.To,
    serviceId,
    cancellationToken);

return new GetAverageServiceTimeResult(
    From: request.From,
    To: request.To,
    ServiceId: request.ServiceId,
    CompletedServicesCount: averageServiceTime.CompletedServicesCount,
    AverageDurationMinutes: averageServiceTime.AverageDurationMinutes);
```

- [ ] **Step 5: Update Infrastructure average query**

Modify `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`.

Add usings:

```csharp
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Enums;
```

Replace `GetAverageServiceTimeAsync` with:

```csharp
public async Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
    DateTime completedFrom,
    DateTime completedTo,
    ServiceId? serviceId = null,
    CancellationToken cancellationToken = default)
{
    var query = _dbContext.Set<EstimateServiceLine>()
        .AsNoTracking()
        .Where(line =>
            line.Status == EstimateServiceLineStatus.Completed &&
            line.StartedAt.HasValue &&
            line.CompletedAt.HasValue &&
            line.CompletedAt.Value >= completedFrom &&
            line.CompletedAt.Value <= completedTo);

    if (serviceId is not null)
    {
        query = query.Where(line => line.ServiceId == serviceId);
    }

    var completedServicesCount = await query.CountAsync(cancellationToken);
    if (completedServicesCount == 0)
    {
        return new AverageServiceTimeReadModel(
            CompletedServicesCount: 0,
            AverageDurationMinutes: null);
    }

    var averageDurationMinutes = await query.AverageAsync(
        line => (line.CompletedAt!.Value - line.StartedAt!.Value).TotalMinutes,
        cancellationToken);

    return new AverageServiceTimeReadModel(
        CompletedServicesCount: completedServicesCount,
        AverageDurationMinutes: averageDurationMinutes);
}
```

- [ ] **Step 6: Update API contract and binding**

Modify `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResponse.cs`:

```csharp
namespace GarageFlow.Api.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResponse(
    DateTime From,
    DateTime To,
    Guid? ServiceId,
    int CompletedServicesCount,
    double? AverageDurationMinutes);
```

Modify endpoint method signature:

```csharp
private static async Task<IResult> GetAverageServiceTime(
    DateTimeOffset from,
    DateTimeOffset to,
    Guid? serviceId,
    IMediator mediator,
    CancellationToken cancellationToken)
```

Send query:

```csharp
new GetAverageServiceTimeQuery(from.UtcDateTime, to.UtcDateTime, serviceId)
```

Map response:

```csharp
var response = new GetAverageServiceTimeResponse(
    From: result.From,
    To: result.To,
    ServiceId: result.ServiceId,
    CompletedServicesCount: result.CompletedServicesCount,
    AverageDurationMinutes: result.AverageDurationMinutes);
```

Update summary:

```csharp
.WithSummary("Get average execution time for completed estimate services")
```

- [ ] **Step 7: Update integration contract**

Modify `Tests/Integration/Api/WorkOrders/Contracts/AverageServiceTimeResponse.cs`:

```csharp
namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed class AverageServiceTimeResponse
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public Guid? ServiceId { get; init; }
    public int CompletedServicesCount { get; init; }
    public double? AverageDurationMinutes { get; init; }
}
```

- [ ] **Step 8: Write repository integration tests for filtering**

In `Tests/Integration/WorkOrders/WorkOrderRepositoryQueryTests.cs`, rewrite average tests to create completed service lines through the aggregate.

Add helper:

```csharp
private static WorkOrder CreateWorkOrderWithCompletedService(
    GarageFlowDbContext dbContext,
    CustomerId customerId,
    VehicleId vehicleId,
    ServiceId serviceId,
    DateTime startedAt,
    DateTime completedAt)
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
    workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);
    dbContext.WorkOrders.Add(workOrder);
    dbContext.Entry(serviceLine).Property(line => line.StartedAt).CurrentValue = startedAt;
    dbContext.Entry(serviceLine).Property(line => line.CompletedAt).CurrentValue = completedAt;
    dbContext.Entry(workOrder).Property(item => item.StartedAt).CurrentValue = startedAt;
    dbContext.Entry(workOrder).Property(item => item.CompletedAt).CurrentValue = completedAt;
    return workOrder;
}
```

Add test:

```csharp
[Fact]
public async Task GetAverageServiceTimeAsync_ShouldFilterCompletedServiceLinesByServiceId()
{
    using var dbContext = CreateDbContext();
    var repository = new WorkOrderRepository(dbContext);
    var customerId = CustomerId.New();
    var vehicleId = VehicleId.New();
    var targetServiceId = ServiceId.New();
    var otherServiceId = ServiceId.New();
    var from = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
    var to = new DateTime(2026, 5, 2, 23, 59, 59, DateTimeKind.Utc);
    CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, targetServiceId, from.AddHours(8), from.AddHours(9));
    CreateWorkOrderWithCompletedService(dbContext, customerId, vehicleId, otherServiceId, from.AddHours(10), from.AddHours(12));
    await dbContext.SaveChangesAsync();

    var result = await repository.GetAverageServiceTimeAsync(from, to, targetServiceId);

    Assert.Equal(1, result.CompletedServicesCount);
    Assert.Equal(60d, result.AverageDurationMinutes);
}
```

- [ ] **Step 9: Run average tests to verify GREEN**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "GetAverageServiceTime"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "GetAverageServiceTime|AverageServiceTime"
```

Expected: average unit and integration tests pass using service-line counts.

- [ ] **Step 10: Commit Task 6**

Run:

```bash
git add Domain/WorkOrders/Repositories Application/WorkOrders/GetAverageServiceTime Api/WorkOrders/GetAverageServiceTime Infrastructure/WorkOrders/Repositories Tests/Unit/WorkOrders Tests/Integration
git commit -m "feat: calculate average duration from service lines"
```

---

### Task 7: API Workflow Coverage And Final Validation

**Files:**

- Modify: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`
- Modify: `Tests/Integration/Api/WorkOrders/Contracts/*.cs`
- Modify: any changed source file surfaced by failing full tests.

- [ ] **Step 1: Add remaining API behavior tests**

Add tests in `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs` for:

```csharp
[Fact]
public async Task StartEstimateService_ShouldReturn403_WhenAuthenticatedUserIsCustomer()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var customerClient = await app.CreateCustomerClientAsync();

    var response = await customerClient.PostAsync(
        $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/start",
        content: null);

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
}

[Fact]
public async Task CompleteEstimateService_ShouldReturn403_WhenAuthenticatedUserIsCustomer()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var customerClient = await app.CreateCustomerClientAsync();

    var response = await customerClient.PostAsync(
        $"/work-orders/{Guid.NewGuid()}/estimates/{Guid.NewGuid()}/services/{Guid.NewGuid()}/complete",
        content: null);

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
}

[Fact]
public async Task Staff_ShouldReceive409_WhenSubmittingInventoryOnlyEstimate()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var staffClient = await app.CreateStaffClientAsync();
    var workOrder = await CreateWorkOrderAsync(staffClient);
    var estimate = await CreateEstimateAsync(staffClient, workOrder.Id);
    var inventoryItem = await CreateInventoryItemAsync(staffClient);
    var addInventoryResponse = await staffClient.PostAsJsonAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/inventory-items",
        new AddEstimateInventoryItemRequest(inventoryItem.Id, Quantity: 1));
    HttpResponseAssertions.AssertStatus(addInventoryResponse, HttpStatusCode.OK);

    var submitResponse = await staffClient.PostAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/submit",
        content: null);

    HttpResponseAssertions.AssertStatus(submitResponse, HttpStatusCode.Conflict);
}
```

Use existing helper names in `WorkOrdersApiTests.cs`; if `CreateInventoryItemAsync` has a different signature, call the local inventory helper already used in that test file.

- [ ] **Step 2: Run added API tests to verify RED or GREEN**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "StartEstimateService_ShouldReturn403|CompleteEstimateService_ShouldReturn403|Staff_ShouldReceive409_WhenSubmittingInventoryOnlyEstimate"
```

Expected: authorization tests pass if Task 4 registered routes under staff policy. Inventory-only test passes if Task 1 submission invariant is active. If any test fails, fix the matching endpoint registration or invariant.

- [ ] **Step 3: Update existing average API tests**

In `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`, replace assertions:

```csharp
payload.CompletedWorkOrdersCount
```

with:

```csharp
payload.CompletedServicesCount
```

Update `CreateAverageServiceTimeUrl` overload to accept optional service id:

```csharp
private static string CreateAverageServiceTimeUrl(DateTimeOffset from, DateTimeOffset to, Guid? serviceId = null)
{
    var url = $"/work-orders/average-service-time?from={FormatOffset(from)}&to={FormatOffset(to)}";
    return serviceId.HasValue ? $"{url}&serviceId={serviceId.Value}" : url;
}
```

Add API test:

```csharp
[Fact]
public async Task AverageServiceTime_ShouldFilterByServiceId_WhenProvided()
{
    await using var app = await GarageFlowWebApplicationFactory.CreateAsync();
    using var staffClient = await app.CreateStaffClientAsync();
    var from = DateTimeOffset.UtcNow.AddMinutes(-10);
    var to = DateTimeOffset.UtcNow.AddMinutes(10);
    var targetService = await CreateServiceAsync(staffClient, new ServiceBuilder().WithDescription($"Target average {Guid.NewGuid():N}"));
    var otherService = await CreateServiceAsync(staffClient, new ServiceBuilder().WithDescription($"Other average {Guid.NewGuid():N}"));
    await CompleteWorkOrderServiceThroughApiAsync(app, staffClient, targetService.Id);
    await CompleteWorkOrderServiceThroughApiAsync(app, staffClient, otherService.Id);

    var response = await staffClient.GetAsync(CreateAverageServiceTimeUrl(from, to, targetService.Id));

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<AverageServiceTimeResponse>(response);
    Assert.Equal(targetService.Id, payload.ServiceId);
    Assert.Equal(1, payload.CompletedServicesCount);
    Assert.NotNull(payload.AverageDurationMinutes);
}
```

Add helper:

```csharp
private static async Task CompleteWorkOrderServiceThroughApiAsync(
    GarageFlowWebApplicationFactory app,
    HttpClient staffClient,
    Guid serviceId)
{
    var workOrder = await CreateWorkOrderAsync(staffClient);
    var estimate = await CreateEstimateAsync(staffClient, workOrder.Id);
    var addServiceResponse = await staffClient.PostAsJsonAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services",
        new AddEstimateServiceRequest(serviceId));
    HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);
    var addServicePayload = await HttpResponseAssertions.ReadRequiredJsonAsync<AddEstimateServiceResponse>(addServiceResponse);
    await SubmitAndApproveEstimateAsync(app, staffClient, workOrder.Id, estimate.Id);
    var startResponse = await staffClient.PostAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{addServicePayload.Id}/start",
        content: null);
    HttpResponseAssertions.AssertStatus(startResponse, HttpStatusCode.NoContent);
    var completeResponse = await staffClient.PostAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services/{addServicePayload.Id}/complete",
        content: null);
    HttpResponseAssertions.AssertStatus(completeResponse, HttpStatusCode.NoContent);
}
```

- [ ] **Step 4: Run API work-order integration tests**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Integration.Api.WorkOrders.WorkOrdersApiTests"
```

Expected: all WorkOrders API integration tests pass.

- [ ] **Step 5: Run full required validation**

Run:

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test GarageFlow.slnx
```

Expected: build and all tests pass.

- [ ] **Step 6: Commit Task 7**

Run:

```bash
git add .
git commit -m "test: cover service-line work order workflow"
```

---

## Self-Review

Spec coverage:

- Domain service-line state machine: Tasks 1 and 2.
- Start/complete service endpoints: Tasks 3, 4, 5, and 7.
- Work order completion from all approved estimate services: Tasks 1, 5, and 7.
- Estimate requires at least one service for approval: Tasks 1 and 7.
- Mocked approval email logging: Task 3.
- Average duration by service-line timing with optional service id: Task 6 and Task 7.
- Read models and response status/timing fields: Task 5.
- EF mapping and migration: Task 5.
- Final AGENTS.md validation commands: Task 7.

Red-flag scan:

- The plan contains no incomplete sections or unassigned implementation areas.

Type consistency:

- The plan uses `EstimateServiceLineStatus`, `StartEstimateServiceCommand`, `CompleteEstimateServiceCommand`, `ICustomerApprovalEmailSender`, `CompletedServicesCount`, and optional `ServiceId` consistently across Domain, Application, API, Infrastructure, and tests.
