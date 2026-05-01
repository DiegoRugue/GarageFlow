# Average Service Time Design

## Context

GarageFlow already has a `WorkOrders` module with a staff workflow for creating a work order, creating and submitting estimates, customer approval, starting work, completing the work order, and delivery.

The new requirement is to monitor the average service duration for work orders. Staff must be able to choose a time window, and the API must return the average duration for work orders completed inside that window.

The feature must follow the existing GarageFlow architecture:

- keep lifecycle rules inside the `WorkOrder` aggregate;
- keep Minimal API endpoints thin;
- use Application handlers for orchestration and validation;
- use Infrastructure repositories for persistence queries;
- keep customer-facing endpoints out of this staff metric;
- protect the endpoint with the existing `ActiveStaff` policy.

## Decision

Use timestamps on the `WorkOrder` aggregate:

- `StartedAt`
- `CompletedAt`

`StartedAt` is set when staff starts real service work through `StartWork()`.

`CompletedAt` is set when staff completes the work order through `Complete()`.

The metric filters work orders by completion time:

```text
CompletedAt >= from
CompletedAt <= to
```

All service timing timestamps and query windows are UTC. API clients should send ISO-8601 values with an explicit UTC offset, preferably `Z`.

The average duration is calculated only for work orders that have both timestamps:

```text
CompletedAt - StartedAt
```

This keeps the model simple, auditable, and aligned with the existing state machine. It intentionally measures the active service execution interval, not diagnosis, estimate preparation, waiting for customer approval, delivery, or total shop cycle time.

## Domain Model

Update `Domain/WorkOrders/Entities/WorkOrder.cs`.

Add nullable properties:

```csharp
public DateTime? StartedAt { get; private set; }
public DateTime? CompletedAt { get; private set; }
```

`StartDiagnosis()` does not change service timing.

`StartWork()` transitions the work order to `InProgress` and sets `StartedAt` when the transition succeeds.

`Complete()` requires exactly one approved estimate, transitions the work order to `Completed`, and sets `CompletedAt` when the transition succeeds.

The existing state machine remains the source of truth:

```text
Approved -> InProgress -> Completed
```

The timestamps should not be overwritten by no-op transitions. The current `TransitionTo()` implementation returns early when the requested status is already current; timing logic should preserve that behavior.

For legacy or inconsistent rows, the average service time query ignores completed work orders missing either timestamp.

## Application Use Case

Create a new vertical slice:

```text
Application/WorkOrders/GetAverageServiceTime
```

Files:

```text
GetAverageServiceTimeQuery.cs
GetAverageServiceTimeHandler.cs
GetAverageServiceTimeResult.cs
```

Query contract:

```csharp
public sealed record GetAverageServiceTimeQuery(DateTime From, DateTime To)
    : IRequest<GetAverageServiceTimeResult>;
```

Result contract:

```csharp
public sealed record GetAverageServiceTimeResult(
    DateTime From,
    DateTime To,
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
```

Handler rules:

- `From` must be earlier than `To`;
- `From` and `To` represent a UTC window;
- invalid windows throw `ValidationException`;
- query handlers do not open transactions;
- the handler delegates the metric calculation to `IWorkOrderRepository`;
- the handler returns `AverageDurationMinutes = null` when the window has no matching completed work orders.

## Repository And Read Model

Add a read model under `Domain/WorkOrders/Repositories`:

```csharp
public sealed record AverageServiceTimeReadModel(
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
```

Extend `IWorkOrderRepository` with:

```csharp
Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
    DateTime from,
    DateTime to,
    CancellationToken cancellationToken = default);
```

The Infrastructure implementation filters `WorkOrders` with:

```text
StartedAt != null
CompletedAt != null
CompletedAt >= from
CompletedAt <= to
```

It returns:

- count of matching completed work orders;
- average duration in minutes;
- `null` average when count is zero.

## API Endpoint

Create a new staff endpoint vertical slice:

```text
Api/WorkOrders/GetAverageServiceTime
```

Files:

```text
GetAverageServiceTimeEndpoint.cs
GetAverageServiceTimeResponse.cs
```

Route:

```text
GET /work-orders/average-service-time?from=2026-05-01T00:00:00Z&to=2026-05-31T23:59:59Z
```

Response:

```json
{
  "from": "2026-05-01T00:00:00Z",
  "to": "2026-05-31T23:59:59Z",
  "completedWorkOrdersCount": 12,
  "averageDurationMinutes": 184.5
}
```

No matching completed work orders:

```json
{
  "from": "2026-05-01T00:00:00Z",
  "to": "2026-05-31T23:59:59Z",
  "completedWorkOrdersCount": 0,
  "averageDurationMinutes": null
}
```

Endpoint metadata:

- `.WithName("GetAverageServiceTime")`
- `.WithTags("Work Orders")`
- `.WithSummary("Get average service time for completed work orders")`
- `.Produces<GetAverageServiceTimeResponse>(StatusCodes.Status200OK)`
- `.ProducesProblem(StatusCodes.Status400BadRequest)`
- `.ProducesProblem(StatusCodes.Status401Unauthorized)`
- `.ProducesProblem(StatusCodes.Status403Forbidden)`
- `.ProducesProblem(StatusCodes.Status500InternalServerError)`

Register the endpoint in `Api/WorkOrders/WorkOrderEndpoints.cs` under the existing staff route group. The route group already applies `SecurityPolicies.ActiveStaff`.

## Persistence

Update `Infrastructure/WorkOrders/Configurations/WorkOrderEntityConfiguration.cs`.

Map:

```csharp
builder.Property(workOrder => workOrder.StartedAt);
builder.Property(workOrder => workOrder.CompletedAt);
```

Add an index for the completion-window query:

```csharp
builder.HasIndex(workOrder => workOrder.CompletedAt);
```

The existing `WorkOrders` table gains nullable columns:

```text
StartedAt
CompletedAt
```

Nullable columns allow existing work orders to remain valid and let tests using the InMemory provider handle rows without timing data.

## Testing Strategy

### Unit Tests: Domain

Add scenarios to `Tests/Unit/WorkOrders/WorkOrderTests.cs`:

- `StartWork` sets `StartedAt`;
- `StartDiagnosis` leaves `StartedAt` and `CompletedAt` empty;
- `Complete` sets `CompletedAt`;
- completed work order has `CompletedAt` greater than or equal to `StartedAt`;
- attempting invalid lifecycle transitions does not set timing fields.

### Unit Tests: Application

Add scenarios to `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`:

- handler throws `ValidationException` when `from >= to`;
- handler returns count and average duration from the repository;
- handler returns `AverageDurationMinutes = null` when repository count is zero;
- handler does not use `IUnitOfWork`.

### Integration Tests

Add scenarios to `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`:

- staff can complete a work order and retrieve a non-null average for a window containing `CompletedAt`;
- a valid window with no completed work orders returns count `0` and `averageDurationMinutes = null`;
- unauthenticated requests return `401`;
- customer-authenticated requests return `403`;
- invalid windows return `400`.

The integration flow should create a work order, create an estimate, add at least one line, submit it, approve it as the customer, start work as staff, complete it as staff, and query the average service time.

## Out Of Scope

- technician assignment;
- per-technician average duration;
- average total shop cycle time from creation to delivery;
- diagnosis duration;
- waiting-for-approval duration;
- persistent status history or event-sourced reporting;
- dashboard UI;
- background aggregation tables.

## Acceptance Criteria

- `WorkOrder.StartWork()` records the service start time.
- `WorkOrder.Complete()` records the service completion time.
- The new staff endpoint returns average duration for work orders completed inside the requested window.
- The metric ignores work orders without both timing timestamps.
- Empty windows return `200 OK`, count `0`, and null average.
- Invalid windows return `400 Bad Request`.
- The endpoint is available only to active staff.
- API types do not depend directly on Domain, Infrastructure, or BuildingBlocks.
- The implementation follows the canonical GarageFlow vertical-slice layout.
- The implementation keeps code duplication below the CI threshold of 3%.

## Verification

Final validation must follow the CI quality gate:

- build completes with zero warnings;
- code coverage stays above 80%;
- duplicated code stays below 3%.

Final local validation must run:

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test GarageFlow.slnx
```

During implementation, keep shared test setup and mapping helpers aligned with existing patterns to avoid unnecessary duplication while still preserving clear vertical slices.
