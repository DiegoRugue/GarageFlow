# Work Order Service State Machine Design

## Context

GarageFlow currently completes a work order through a direct staff endpoint:

```text
POST /work-orders/{id}/complete
```

That no longer matches the business process. A work order is complete only when every service being performed in its approved estimate is complete.

The existing average service time endpoint also measures work-order-level timing with `WorkOrder.StartedAt` and `WorkOrder.CompletedAt`. The new requirement is to measure the execution time of each service line in approved estimates, optionally filtered by a service catalog identifier.

The design must keep GarageFlow's existing architecture boundaries:

- domain invariants remain inside the `WorkOrder` aggregate and its child entities;
- Minimal API endpoints stay thin and depend only on Application contracts;
- handlers orchestrate use cases and transactions;
- Infrastructure owns EF mapping, migrations, repository queries, and the mocked email sender;
- staff-only endpoints stay under the existing `SecurityPolicies.ActiveStaff` route group;
- customer approval endpoints stay under the existing `SecurityPolicies.ActiveCustomer` route group.

## Decision

Use `EstimateServiceLine` as the execution unit for work performed on a work order.

Each service line receives its own state machine and timing fields:

```text
Pending -> InProgress -> Completed
```

```csharp
public EstimateServiceLineStatus Status { get; private set; }
public DateTime? StartedAt { get; private set; }
public DateTime? CompletedAt { get; private set; }
```

The work order is no longer completed directly. Staff starts and completes individual service lines through dedicated endpoints:

```text
POST /work-orders/{id}/estimates/{estimateId}/services/{lineId}/start
POST /work-orders/{id}/estimates/{estimateId}/services/{lineId}/complete
```

Starting the first approved service line moves the work order to `InProgress` and sets `WorkOrder.StartedAt` if it was not already set.

Completing a service line records the service line `CompletedAt`. After each service completion, the aggregate checks the approved estimate. If all approved estimate service lines are `Completed`, the work order transitions to `Completed` and sets `WorkOrder.CompletedAt`.

Submitting an estimate for customer approval now requires at least one service line. Inventory-only estimates cannot be submitted.

When submitting an estimate moves the work order to `WaitingApproval`, the Application handler calls a mocked email sender. The mock implementation logs that an approval email was sent to the customer.

## Domain Model

Add an enum under `Domain/WorkOrders/Enums`:

```csharp
public enum EstimateServiceLineStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3
}
```

Update `Domain/WorkOrders/Entities/EstimateServiceLine.cs`.

New properties:

```csharp
public EstimateServiceLineStatus Status { get; private set; }
public DateTime? StartedAt { get; private set; }
public DateTime? CompletedAt { get; private set; }
```

Creation sets the line to `Pending`.

Service line behavior uses a private `TransitionTo` helper, matching the existing aggregate style for explicit state transitions:

```csharp
internal void Start()
internal void Complete()
private void TransitionTo(EstimateServiceLineStatus newStatus, DateTime occurredAt)
```

`Start()` accepts only `Pending -> InProgress`, sets `StartedAt`, and updates `UpdatedAt`.

`Complete()` accepts only `InProgress -> Completed`, sets `CompletedAt`, and updates `UpdatedAt`.

Update `Domain/WorkOrders/Entities/WorkOrder.cs`.

Add aggregate methods:

```csharp
public void StartEstimateService(EstimateId estimateId, EstimateServiceLineId lineId)
public void CompleteEstimateService(EstimateId estimateId, EstimateServiceLineId lineId)
```

`StartEstimateService` rules:

- the work order must not be `Completed`, `Delivered`, or `Cancelled`;
- the target estimate must exist;
- the target estimate must be `Approved`;
- the target service line must exist on that estimate;
- the service line must be `Pending`;
- if the work order is `Approved`, transition it to `InProgress`;
- if the work order is already `InProgress`, keep the existing work-order `StartedAt`;
- set the service line `StartedAt`.

`CompleteEstimateService` rules:

- the work order must be `InProgress`;
- the target estimate must exist;
- the target estimate must be `Approved`;
- the target service line must exist on that estimate;
- the service line must be `InProgress`;
- set the service line `CompletedAt`;
- if every service line in the approved estimate is `Completed`, transition the work order to `Completed` and set `WorkOrder.CompletedAt`.

Remove direct completion from the Application and API public surface. The aggregate keeps private helpers for `Approved -> InProgress -> Completed`, but Application code must complete a work order only by calling `CompleteEstimateService`.

Update estimate submission validation:

```text
Estimate must contain at least one service line before submission.
```

Inventory lines can still be part of an estimate, but they no longer satisfy the minimum requirement for sending approval to the customer.

## API Endpoints

Add staff endpoints:

```text
Api/WorkOrders/StartEstimateService/StartEstimateServiceEndpoint.cs
Api/WorkOrders/CompleteEstimateService/CompleteEstimateServiceEndpoint.cs
```

Routes:

```text
POST /work-orders/{id:guid}/estimates/{estimateId:guid}/services/{lineId:guid}/start
POST /work-orders/{id:guid}/estimates/{estimateId:guid}/services/{lineId:guid}/complete
```

Both endpoints return `204 NoContent` on success.

Both endpoints must define:

- `.WithName(...)`
- `.WithTags("Work Orders")`
- `.WithSummary(...)`
- `.Produces(StatusCodes.Status204NoContent)`
- `.ProducesProblem(StatusCodes.Status400BadRequest)`
- `.ProducesProblem(StatusCodes.Status401Unauthorized)`
- `.ProducesProblem(StatusCodes.Status403Forbidden)`
- `.ProducesProblem(StatusCodes.Status404NotFound)`
- `.ProducesProblem(StatusCodes.Status409Conflict)`
- `.ProducesProblem(StatusCodes.Status500InternalServerError)`

Register both endpoints in `Api/WorkOrders/WorkOrderEndpoints.cs` under the staff route group.

Remove registration for:

```text
POST /work-orders/{id:guid}/complete
```

Delete the `CompleteWorkOrder` endpoint, command, and handler.

## Application Use Cases

Create two vertical slices:

```text
Application/WorkOrders/StartEstimateService
Application/WorkOrders/CompleteEstimateService
```

Contracts:

```csharp
public sealed record StartEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : IRequest<Unit>;

public sealed record CompleteEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : IRequest<Unit>;
```

Handler flow:

```text
BeginTransactionAsync
load work order with GetByIdForEstimateMutationAsync
throw NotFoundException when missing
call aggregate method
CommitTransactionAsync
RollbackTransactionAsync in catch
```

Handlers should not implement lifecycle rules. They only normalize identifiers, load the aggregate, call domain behavior, and manage the transaction.

## Mocked Approval Email

Add an Application abstraction:

```text
Application/WorkOrders/Abstractions/ICustomerApprovalEmailSender.cs
```

Contract:

```csharp
public interface ICustomerApprovalEmailSender
{
    Task SendEstimateWaitingApprovalAsync(
        Guid workOrderId,
        Guid estimateId,
        Guid customerId,
        CancellationToken cancellationToken);
}
```

Add an Infrastructure implementation:

```text
Infrastructure/WorkOrders/Email/LoggingCustomerApprovalEmailSender.cs
```

The implementation only logs that the email was sent:

```text
Approval email sent to customer {CustomerId} for work order {WorkOrderId} and estimate {EstimateId}.
```

Update `SubmitEstimateHandler` to call the sender after `workOrder.SubmitEstimate(estimateId)` when submission moves the work order to `WaitingApproval`, and before committing the transaction.

Register the email sender in Infrastructure dependency injection alongside other Infrastructure services.

## Average Service Time

Keep the existing route and evolve the contract:

```text
GET /work-orders/average-service-time?from=2026-05-01T00:00:00Z&to=2026-05-31T23:59:59Z
GET /work-orders/average-service-time?from=2026-05-01T00:00:00Z&to=2026-05-31T23:59:59Z&serviceId={serviceId}
```

`serviceId` is optional.

Without `serviceId`, the endpoint returns the average duration for all completed service lines in the time window.

With `serviceId`, the endpoint returns the average duration only for completed service lines whose `ServiceId` matches the filter.

The query uses service-line timing:

```text
EstimateServiceLine.Status == Completed
EstimateServiceLine.StartedAt != null
EstimateServiceLine.CompletedAt != null
EstimateServiceLine.CompletedAt >= from
EstimateServiceLine.CompletedAt <= to
optional EstimateServiceLine.ServiceId == serviceId
```

Duration:

```text
EstimateServiceLine.CompletedAt - EstimateServiceLine.StartedAt
```

Update Application query and result:

```csharp
public sealed record GetAverageServiceTimeQuery(
    DateTime From,
    DateTime To,
    Guid? ServiceId) : IRequest<GetAverageServiceTimeResult>;

public sealed record GetAverageServiceTimeResult(
    DateTime From,
    DateTime To,
    Guid? ServiceId,
    int CompletedServicesCount,
    double? AverageDurationMinutes);
```

Update API response:

```csharp
public sealed record GetAverageServiceTimeResponse(
    DateTime From,
    DateTime To,
    Guid? ServiceId,
    int CompletedServicesCount,
    double? AverageDurationMinutes);
```

Example response:

```json
{
  "from": "2026-05-01T00:00:00Z",
  "to": "2026-05-31T23:59:59Z",
  "serviceId": null,
  "completedServicesCount": 12,
  "averageDurationMinutes": 184.5
}
```

No matching service lines:

```json
{
  "from": "2026-05-01T00:00:00Z",
  "to": "2026-05-31T23:59:59Z",
  "serviceId": "2e808bb8-9fd6-4c27-840b-85998f524016",
  "completedServicesCount": 0,
  "averageDurationMinutes": null
}
```

Rename the repository read model fields to match service-line semantics:

```csharp
public sealed record AverageServiceTimeReadModel(
    int CompletedServicesCount,
    double? AverageDurationMinutes);
```

Extend `IWorkOrderRepository.GetAverageServiceTimeAsync`:

```csharp
Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
    DateTime completedFrom,
    DateTime completedTo,
    ServiceId? serviceId = null,
    CancellationToken cancellationToken = default);
```

## Read Models And Responses

Update `WorkOrderServiceLineReadModel`, Application DTOs, API responses, and integration-test contracts to include:

```text
status
startedAt
completedAt
```

Both staff and customer detail endpoints should expose these values so clients can see service execution progress.

The API layer must map strings and nullable dates from Application DTOs and must not reference Domain enums directly.

## Persistence

Update `Infrastructure/WorkOrders/Configurations/EstimateServiceLineEntityConfiguration.cs`.

Map:

```csharp
builder.Property(line => line.Status)
    .HasConversion<string>()
    .HasMaxLength(32)
    .IsRequired();

builder.Property(line => line.StartedAt);
builder.Property(line => line.CompletedAt);

builder.HasIndex(line => line.ServiceId);
builder.HasIndex(line => line.CompletedAt);
builder.HasIndex(line => new { line.ServiceId, line.CompletedAt });
```

Add an EF migration to add:

```text
Status text/varchar not null
StartedAt nullable timestamp
CompletedAt nullable timestamp
```

Existing service lines must be backfilled to `Pending`.

## Testing Strategy

### Unit Tests: Domain

Add or update scenarios in `Tests/Unit/WorkOrders/WorkOrderTests.cs`:

- `EstimateServiceLine` starts with `Pending` status;
- starting a pending service line sets `InProgress` and `StartedAt`;
- completing an in-progress service line sets `Completed` and `CompletedAt`;
- completing a pending service line throws `BusinessRuleViolationException`;
- starting a completed service line throws `BusinessRuleViolationException`;
- starting the first service line moves the work order from `Approved` to `InProgress`;
- starting a second service line keeps the original `WorkOrder.StartedAt`;
- completing one of multiple services keeps the work order `InProgress`;
- completing all services in the approved estimate moves the work order to `Completed`;
- submitting an inventory-only estimate throws `BusinessRuleViolationException`;
- submitting an estimate with at least one service line still succeeds.

### Unit Tests: Application

Add or update scenarios in `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`:

- `StartEstimateServiceHandler` opens a transaction, loads with `GetByIdForEstimateMutationAsync`, starts the service, and commits;
- `StartEstimateServiceHandler` rolls back when the work order is missing or domain validation fails;
- `CompleteEstimateServiceHandler` opens a transaction, loads with `GetByIdForEstimateMutationAsync`, completes the service, and commits;
- `CompleteEstimateServiceHandler` rolls back when the work order is missing or domain validation fails;
- `SubmitEstimateHandler` calls `ICustomerApprovalEmailSender` when the estimate enters `WaitingApproval`;
- `GetAverageServiceTimeHandler` validates `From < To`;
- `GetAverageServiceTimeHandler` passes optional `ServiceId` to the repository;
- `GetAverageServiceTimeHandler` returns `CompletedServicesCount`.

### Repository And API Integration Tests

Add or update scenarios in:

```text
Tests/Integration/WorkOrders/WorkOrderRepositoryQueryTests.cs
Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs
```

Repository query scenarios:

- average returns count `0` and null average when no completed service lines match;
- average includes service-line completion boundaries;
- average excludes rows without both service timing timestamps;
- average computes from service-line `CompletedAt - StartedAt`;
- average filters by `ServiceId` when provided;
- average without `ServiceId` includes all completed service lines.

API scenarios:

- unauthenticated start and complete service requests return `401`;
- customer-authenticated start and complete service requests return `403`;
- staff can start a service and see it `InProgress` in work-order details;
- staff can complete a service and see it `Completed` in work-order details;
- completing all services moves the work order to `Completed`;
- `POST /work-orders/{id}/complete` is no longer registered;
- average service time returns `completedServicesCount`;
- average service time filters by `serviceId`;
- invalid average windows return `400`;
- inventory-only estimate submission returns `409`.

## Out Of Scope

- assigning technicians to service lines;
- pausing or resuming service execution;
- reopening completed services;
- persistent status history beyond current aggregate fields and domain events;
- real SMTP, templates, or background email delivery;
- dashboard UI;
- per-technician metrics.

## Acceptance Criteria

- Work orders can no longer be completed through a direct complete endpoint.
- Service execution is controlled through start and complete service-line endpoints.
- Service lines enforce `Pending -> InProgress -> Completed`.
- Starting a service line records `StartedAt`.
- Completing a service line records `CompletedAt`.
- A work order becomes `Completed` only when all service lines in its approved estimate are `Completed`.
- An estimate must contain at least one service line before it can be submitted for customer approval.
- Submitting an estimate to waiting approval calls a mocked email sender that logs the email event.
- Average service time uses service-line timings.
- Average service time accepts an optional `serviceId` filter.
- Average service time response uses `CompletedServicesCount`, not `CompletedWorkOrdersCount`.
- Staff and customer work-order details expose service-line status and timing fields.
- API runtime types do not depend directly on Domain, Infrastructure, or BuildingBlocks.
- The implementation follows the canonical GarageFlow vertical-slice layout.

## Verification

Final validation must run:

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test GarageFlow.slnx
```

During implementation, focused filters are acceptable for TDD red-green cycles, but final validation must include the full commands above.
