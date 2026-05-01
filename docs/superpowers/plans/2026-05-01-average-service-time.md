# Average Service Time Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add service-duration tracking to work orders and expose a staff-only endpoint that returns average service time for work orders completed inside a requested UTC window.

**Architecture:** Store `StartedAt` and `CompletedAt` on the `WorkOrder` aggregate, set them during existing lifecycle transitions, and query the metric through the existing `IWorkOrderRepository` abstraction. Keep the API as a thin Minimal API adapter and keep all validation/orchestration in the Application slice.

**Tech Stack:** C#/.NET 10, ASP.NET Core Minimal APIs, Mediator, EF Core 10, xUnit, Moq, coverlet, Sonar quality gate expectations.

---

## File Structure

Create:

- `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeQuery.cs` - request contract for the metric query.
- `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeHandler.cs` - validates the UTC window and delegates to the repository.
- `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResult.cs` - Application result returned to the API.
- `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeEndpoint.cs` - staff Minimal API endpoint.
- `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResponse.cs` - API response contract.
- `Domain/WorkOrders/Repositories/AverageServiceTimeReadModel.cs` - repository read model.
- `Tests/Integration/Api/WorkOrders/Contracts/AverageServiceTimeResponse.cs` - integration-test response contract.

Modify:

- `Domain/WorkOrders/Entities/WorkOrder.cs` - add timing fields and set them in lifecycle behavior.
- `Domain/WorkOrders/Repositories/IWorkOrderRepository.cs` - add metric query contract.
- `Infrastructure/WorkOrders/Configurations/WorkOrderEntityConfiguration.cs` - map timing fields and index `CompletedAt`.
- `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs` - implement average-duration query.
- `Api/WorkOrders/WorkOrderEndpoints.cs` - register the staff endpoint.
- `Tests/Unit/WorkOrders/WorkOrderTests.cs` - domain timestamp coverage.
- `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs` - Application handler coverage and repository mock extension.
- `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs` - endpoint behavior, auth, and metric flow.

Keep changes scoped to the existing `WorkOrders` vertical slice. Do not expose the timing fields in existing work-order details responses unless a future requirement asks for that.

---

### Task 1: Domain Service Timing

**Files:**
- Modify: `Domain/WorkOrders/Entities/WorkOrder.cs`
- Test: `Tests/Unit/WorkOrders/WorkOrderTests.cs`

- [ ] **Step 1: Write failing domain tests**

Add these tests inside `WorkOrderTests`:

```csharp
[Fact]
public void StartDiagnosis_ShouldNotSetServiceTiming()
{
    var workOrder = new WorkOrderBuilder().BuildCreated();

    workOrder.StartDiagnosis();

    Assert.Equal(WorkOrderStatus.Diagnosing, workOrder.Status);
    Assert.Null(workOrder.StartedAt);
    Assert.Null(workOrder.CompletedAt);
}

[Fact]
public void StartWork_ShouldSetStartedAt_WhenTransitioningToInProgress()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    var beforeStart = DateTime.UtcNow;

    workOrder.StartWork();

    var afterStart = DateTime.UtcNow;
    Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
    Assert.NotNull(workOrder.StartedAt);
    Assert.InRange(workOrder.StartedAt.Value, beforeStart, afterStart);
    Assert.Null(workOrder.CompletedAt);
}

[Fact]
public void Complete_ShouldSetCompletedAt_WhenTransitioningToCompleted()
{
    var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
    workOrder.StartWork();
    var beforeComplete = DateTime.UtcNow;

    workOrder.Complete();

    var afterComplete = DateTime.UtcNow;
    Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
    Assert.NotNull(workOrder.StartedAt);
    Assert.NotNull(workOrder.CompletedAt);
    Assert.InRange(workOrder.CompletedAt.Value, beforeComplete, afterComplete);
    Assert.True(workOrder.CompletedAt.Value >= workOrder.StartedAt.Value);
}

[Fact]
public void StartWork_ShouldNotSetStartedAt_WhenTransitionIsRejected()
{
    var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();

    var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.StartWork());

    Assert.Equal("Work order cannot start while waiting for customer approval.", exception.Message);
    Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
    Assert.Null(workOrder.StartedAt);
    Assert.Null(workOrder.CompletedAt);
}
```

- [ ] **Step 2: Run the focused domain tests and confirm failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderTests"
```

Expected: FAIL because `WorkOrder.StartedAt` and `WorkOrder.CompletedAt` do not exist.

- [ ] **Step 3: Add timing fields and lifecycle behavior**

In `WorkOrder.cs`, add the timing properties immediately after `Status`:

```csharp
public WorkOrderStatus Status { get; private set; }
public DateTime? StartedAt { get; private set; }
public DateTime? CompletedAt { get; private set; }
public IReadOnlyCollection<Estimate> Estimates => _estimates.AsReadOnly();
```

Replace `StartWork()` with:

```csharp
public void StartWork()
{
    if (Status == WorkOrderStatus.WaitingApproval)
    {
        throw new BusinessRuleViolationException("Work order cannot start while waiting for customer approval.");
    }

    if (Status == WorkOrderStatus.InProgress)
    {
        return;
    }

    var startedAt = DateTime.UtcNow;
    TransitionTo(WorkOrderStatus.InProgress, startedAt);
    StartedAt = startedAt;
}
```

Replace `Complete()` with:

```csharp
public void Complete()
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

    var completedAt = DateTime.UtcNow;
    TransitionTo(WorkOrderStatus.Completed, completedAt);
    CompletedAt = completedAt;
}
```

Change the `TransitionTo` signature and timestamp assignment:

```csharp
private void TransitionTo(WorkOrderStatus newStatus, DateTime? occurredAt = null)
{
    if (Status == newStatus)
    {
        return;
    }

    var isAllowed = (Status, newStatus) switch
    {
        (WorkOrderStatus.Created, WorkOrderStatus.Diagnosing) => true,
        (WorkOrderStatus.Diagnosing, WorkOrderStatus.WaitingApproval) => true,
        (WorkOrderStatus.WaitingApproval, WorkOrderStatus.Approved) => true,
        (WorkOrderStatus.Approved, WorkOrderStatus.InProgress) => true,
        (WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => true,
        (WorkOrderStatus.Completed, WorkOrderStatus.Delivered) => true,
        (WorkOrderStatus.Created, WorkOrderStatus.Cancelled) => true,
        (WorkOrderStatus.Diagnosing, WorkOrderStatus.Cancelled) => true,
        (WorkOrderStatus.WaitingApproval, WorkOrderStatus.Cancelled) => true,
        (WorkOrderStatus.Approved, WorkOrderStatus.Cancelled) => true,
        _ => false
    };

    if (!isAllowed)
    {
        throw new BusinessRuleViolationException($"Work order status transition from '{Status}' to '{newStatus}' is not allowed.");
    }

    var previousStatus = Status;
    Status = newStatus;
    UpdatedAt = occurredAt ?? DateTime.UtcNow;

    RaiseDomainEvent(new WorkOrderStatusChanged(
        WorkOrderId: Id,
        PreviousStatus: previousStatus,
        NewStatus: newStatus,
        UpdatedAt: UpdatedAt));
}
```

- [ ] **Step 4: Run focused domain tests and confirm pass**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderTests"
```

Expected: PASS.

- [ ] **Step 5: Commit domain timing**

Run:

```bash
git add Domain/WorkOrders/Entities/WorkOrder.cs Tests/Unit/WorkOrders/WorkOrderTests.cs
git commit -m "feat: track work order service timing"
```

---

### Task 2: Application Query And Repository Metric

**Files:**
- Create: `Domain/WorkOrders/Repositories/AverageServiceTimeReadModel.cs`
- Create: `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeQuery.cs`
- Create: `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeHandler.cs`
- Create: `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResult.cs`
- Modify: `Domain/WorkOrders/Repositories/IWorkOrderRepository.cs`
- Modify: `Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`
- Test: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`

- [ ] **Step 1: Write failing Application handler tests**

Add this using to `WorkOrderHandlersTests.cs`:

```csharp
using GarageFlow.Application.WorkOrders.GetAverageServiceTime;
```

Add these tests inside `WorkOrderHandlersTests`:

```csharp
[Fact]
public async Task GetAverageServiceTime_ShouldThrowValidationException_WhenWindowIsInvalid()
{
    var from = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(
        averageServiceTime: new AverageServiceTimeReadModel(CompletedWorkOrdersCount: 0, AverageDurationMinutes: null));
    var handler = new GetAverageServiceTimeHandler(workOrderRepositoryMock.Object);

    var exception = await Assert.ThrowsAsync<ValidationException>(
        async () => await handler.Handle(new GetAverageServiceTimeQuery(from, from), CancellationToken.None));

    Assert.Equal("From must be earlier than To.", exception.Message);
    workOrderRepositoryMock.Verify(
        x => x.GetAverageServiceTimeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
        Times.Never);
}

[Fact]
public async Task GetAverageServiceTime_ShouldReturnAverageDuration_WhenWindowHasCompletedWorkOrders()
{
    var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    var to = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(
        averageServiceTime: new AverageServiceTimeReadModel(
            CompletedWorkOrdersCount: 3,
            AverageDurationMinutes: 184.5d));
    var handler = new GetAverageServiceTimeHandler(workOrderRepositoryMock.Object);

    var result = await handler.Handle(new GetAverageServiceTimeQuery(from, to), CancellationToken.None);

    Assert.Equal(from, result.From);
    Assert.Equal(to, result.To);
    Assert.Equal(3, result.CompletedWorkOrdersCount);
    Assert.Equal(184.5d, result.AverageDurationMinutes);
    workOrderRepositoryMock.Verify(
        x => x.GetAverageServiceTimeAsync(from, to, It.IsAny<CancellationToken>()),
        Times.Once);
}

[Fact]
public async Task GetAverageServiceTime_ShouldReturnNullAverage_WhenWindowHasNoCompletedWorkOrders()
{
    var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
    var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(
        averageServiceTime: new AverageServiceTimeReadModel(
            CompletedWorkOrdersCount: 0,
            AverageDurationMinutes: null));
    var handler = new GetAverageServiceTimeHandler(workOrderRepositoryMock.Object);

    var result = await handler.Handle(new GetAverageServiceTimeQuery(from, to), CancellationToken.None);

    Assert.Equal(0, result.CompletedWorkOrdersCount);
    Assert.Null(result.AverageDurationMinutes);
}
```

Extend the existing `CreateWorkOrderRepositoryMock` helper signature:

```csharp
private static Mock<IWorkOrderRepository> CreateWorkOrderRepositoryMock(
    List<WorkOrder>? initialWorkOrders = null,
    List<WorkOrderDetailsReadModel>? detailsById = null,
    List<WorkOrderDetailsReadModel>? detailsList = null,
    List<WorkOrderDetailsReadModel>? customerDetailsById = null,
    List<WorkOrderDetailsReadModel>? customerDetailsList = null,
    AverageServiceTimeReadModel? averageServiceTime = null)
```

Add this setup before the `AddAsync` setup in the same helper:

```csharp
repositoryMock
    .Setup(x => x.GetAverageServiceTimeAsync(
        It.IsAny<DateTime>(),
        It.IsAny<DateTime>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(averageServiceTime ?? new AverageServiceTimeReadModel(
        CompletedWorkOrdersCount: 0,
        AverageDurationMinutes: null));
```

- [ ] **Step 2: Run focused handler tests and confirm failure**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderHandlersTests.GetAverageServiceTime"
```

Expected: FAIL because `GetAverageServiceTimeHandler`, `GetAverageServiceTimeQuery`, and `AverageServiceTimeReadModel` do not exist.

- [ ] **Step 3: Implement Application contracts, handler, and repository contract**

Create `Domain/WorkOrders/Repositories/AverageServiceTimeReadModel.cs`:

```csharp
namespace GarageFlow.Domain.WorkOrders.Repositories;

public sealed record AverageServiceTimeReadModel(
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
```

Add this method to `IWorkOrderRepository` before `AddAsync`:

```csharp
Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
    DateTime from,
    DateTime to,
    CancellationToken cancellationToken = default);
```

Create `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeQuery.cs`:

```csharp
using Mediator;

namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeQuery(DateTime From, DateTime To) : IRequest<GetAverageServiceTimeResult>;
```

Create `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResult.cs`:

```csharp
namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResult(
    DateTime From,
    DateTime To,
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
```

Create `Application/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeHandler.cs`:

```csharp
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.WorkOrders.Repositories;
using Mediator;

namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed class GetAverageServiceTimeHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<GetAverageServiceTimeQuery, GetAverageServiceTimeResult>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<GetAverageServiceTimeResult> Handle(GetAverageServiceTimeQuery request, CancellationToken cancellationToken)
    {
        if (request.From >= request.To)
        {
            throw new ValidationException("From must be earlier than To.");
        }

        var averageServiceTime = await _workOrderRepository.GetAverageServiceTimeAsync(
            request.From,
            request.To,
            cancellationToken);

        return new GetAverageServiceTimeResult(
            From: request.From,
            To: request.To,
            CompletedWorkOrdersCount: averageServiceTime.CompletedWorkOrdersCount,
            AverageDurationMinutes: averageServiceTime.AverageDurationMinutes);
    }
}
```

Add this method to `WorkOrderRepository` before `AddAsync`:

```csharp
public async Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
    DateTime from,
    DateTime to,
    CancellationToken cancellationToken = default)
{
    var durations = await _dbContext.WorkOrders
        .AsNoTracking()
        .Where(workOrder =>
            workOrder.StartedAt.HasValue &&
            workOrder.CompletedAt.HasValue &&
            workOrder.CompletedAt.Value >= from &&
            workOrder.CompletedAt.Value <= to)
        .Select(workOrder => new
        {
            StartedAt = workOrder.StartedAt!.Value,
            CompletedAt = workOrder.CompletedAt!.Value
        })
        .ToListAsync(cancellationToken);

    if (durations.Count == 0)
    {
        return new AverageServiceTimeReadModel(
            CompletedWorkOrdersCount: 0,
            AverageDurationMinutes: null);
    }

    var averageDurationMinutes = durations.Average(duration =>
        (duration.CompletedAt - duration.StartedAt).TotalMinutes);

    return new AverageServiceTimeReadModel(
        CompletedWorkOrdersCount: durations.Count,
        AverageDurationMinutes: averageDurationMinutes);
}
```

- [ ] **Step 4: Run focused handler tests and confirm pass**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.WorkOrders.WorkOrderHandlersTests.GetAverageServiceTime"
```

Expected: PASS.

- [ ] **Step 5: Commit Application and repository metric**

Run:

```bash
git add Domain/WorkOrders/Repositories/AverageServiceTimeReadModel.cs Domain/WorkOrders/Repositories/IWorkOrderRepository.cs Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs Application/WorkOrders/GetAverageServiceTime Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs
git commit -m "feat: add average service time query"
```

---

### Task 3: API Endpoint, EF Mapping, And Integration Coverage

**Files:**
- Create: `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeEndpoint.cs`
- Create: `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResponse.cs`
- Create: `Tests/Integration/Api/WorkOrders/Contracts/AverageServiceTimeResponse.cs`
- Modify: `Api/WorkOrders/WorkOrderEndpoints.cs`
- Modify: `Infrastructure/WorkOrders/Configurations/WorkOrderEntityConfiguration.cs`
- Modify: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`

- [ ] **Step 1: Write failing integration tests**

Add these using directives to `WorkOrdersApiTests.cs`:

```csharp
using System.Globalization;
using GarageFlow.Tests.Integration.Api.Services.Contracts;
using GarageFlow.Tests.Shared.Services;
```

Add these tests inside `WorkOrdersApiTests`:

```csharp
[Fact]
public async Task AverageServiceTime_ShouldReturn401_WhenRequestHasNoToken()
{
    using var client = _fixture.CreateClient();
    var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);

    var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
}

[Fact]
public async Task AverageServiceTime_ShouldReturn403_WhenAuthenticatedUserIsCustomer()
{
    using var client = await _fixture.CreateAuthenticatedClientAsync();
    var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
        client,
        customerBuilder: CustomerSeed.CreateUniqueBuilder());

    var activateResponse = await client.PostAsJsonAsync(
        $"/customers/{seededVehicle.CustomerId}/portal-user",
        new ActivateCustomerPortalUserRequest(new DateOnly(1991, 1, 11)));
    HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
    var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

    await AuthenticateCreatedUserAsActiveAsync(
        client,
        customerUser.Email,
        customerUser.FullName,
        customerUser.BirthDate,
        "Customer.Average.Service.Time#123");

    var from = DateTime.UtcNow.AddDays(-1);
    var to = DateTime.UtcNow.AddDays(1);

    var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Forbidden);
}

[Fact]
public async Task AverageServiceTime_ShouldReturn400_WhenWindowIsInvalid()
{
    using var client = await _fixture.CreateAuthenticatedClientAsync();
    var from = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, from));

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.BadRequest);
}

[Fact]
public async Task AverageServiceTime_ShouldReturnNullAverage_WhenWindowHasNoCompletedWorkOrders()
{
    using var client = await _fixture.CreateAuthenticatedClientAsync();
    var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);

    var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<AverageServiceTimeResponse>(response);
    Assert.Equal(0, payload.CompletedWorkOrdersCount);
    Assert.Null(payload.AverageDurationMinutes);
}

[Fact]
public async Task AverageServiceTime_ShouldReturnAverageDuration_ForCompletedWorkOrdersInWindow()
{
    using var client = await _fixture.CreateAuthenticatedClientAsync();
    var seededVehicle = await VehicleSeed.CreateWithDependenciesAsync(
        client,
        customerBuilder: CustomerSeed.CreateUniqueBuilder());
    var workOrder = await CreateWorkOrderAsync(client, seededVehicle.CustomerId, seededVehicle.VehicleId);
    var estimate = await CreateEstimateAsync(client, workOrder.Id);
    var service = await CreateServiceAsync(
        client,
        new ServiceBuilder()
            .WithDescription($"Average service labor {Guid.NewGuid():N}")
            .WithPrice(150m));

    var addServiceResponse = await client.PostAsJsonAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/services",
        new AddEstimateServiceRequest(service.Id));
    HttpResponseAssertions.AssertStatus(addServiceResponse, HttpStatusCode.OK);

    var submitResponse = await client.PostAsync(
        $"/work-orders/{workOrder.Id}/estimates/{estimate.Id}/submit",
        content: null);
    HttpResponseAssertions.AssertStatus(submitResponse, HttpStatusCode.NoContent);

    var activateResponse = await client.PostAsJsonAsync(
        $"/customers/{seededVehicle.CustomerId}/portal-user",
        new ActivateCustomerPortalUserRequest(new DateOnly(1991, 1, 11)));
    HttpResponseAssertions.AssertStatus(activateResponse, HttpStatusCode.Created);
    var customerUser = await HttpResponseAssertions.ReadRequiredJsonAsync<ActivateCustomerPortalUserResponse>(activateResponse);

    await AuthenticateCreatedUserAsActiveAsync(
        client,
        customerUser.Email,
        customerUser.FullName,
        customerUser.BirthDate,
        "Customer.Average.Approve#123");

    var approveResponse = await client.PostAsync(
        $"/me/work-orders/{workOrder.Id}/estimates/{estimate.Id}/approve",
        content: null);
    HttpResponseAssertions.AssertStatus(approveResponse, HttpStatusCode.NoContent);

    await client.AuthenticateAsActiveBootstrapAdminAsync();

    var from = DateTime.UtcNow.AddMinutes(-1);
    var startResponse = await client.PostAsync($"/work-orders/{workOrder.Id}/start-work", content: null);
    HttpResponseAssertions.AssertStatus(startResponse, HttpStatusCode.NoContent);

    var completeResponse = await client.PostAsync($"/work-orders/{workOrder.Id}/complete", content: null);
    HttpResponseAssertions.AssertStatus(completeResponse, HttpStatusCode.NoContent);
    var to = DateTime.UtcNow.AddMinutes(1);

    var response = await client.GetAsync(CreateAverageServiceTimeUrl(from, to));

    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<AverageServiceTimeResponse>(response);
    Assert.Equal(1, payload.CompletedWorkOrdersCount);
    Assert.NotNull(payload.AverageDurationMinutes);
    Assert.True(payload.AverageDurationMinutes >= 0);
}
```

Add these helper methods near the existing private helpers:

```csharp
private static async Task<ServiceResponse> CreateServiceAsync(HttpClient client, ServiceBuilder? builder = null)
{
    var request = (builder ?? new ServiceBuilder()).BuildCreateRequest();
    var response = await client.PostAsJsonAsync("/services", request);
    HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Created);
    return await HttpResponseAssertions.ReadRequiredJsonAsync<ServiceResponse>(response);
}

private static string CreateAverageServiceTimeUrl(DateTime from, DateTime to)
{
    return $"/work-orders/average-service-time?from={FormatUtc(from)}&to={FormatUtc(to)}";
}

private static string FormatUtc(DateTime value)
{
    return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
```

Create `Tests/Integration/Api/WorkOrders/Contracts/AverageServiceTimeResponse.cs`:

```csharp
namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record AverageServiceTimeResponse(
    DateTime From,
    DateTime To,
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
```

- [ ] **Step 2: Run focused integration tests and confirm failure**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Integration.Api.WorkOrders.WorkOrdersApiTests.AverageServiceTime" -- RunConfiguration.MaxCpuCount=1
```

Expected: FAIL because `/work-orders/average-service-time` is not mapped.

- [ ] **Step 3: Implement API endpoint and EF mapping**

Create `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeResponse.cs`:

```csharp
namespace GarageFlow.Api.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResponse(
    DateTime From,
    DateTime To,
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
```

Create `Api/WorkOrders/GetAverageServiceTime/GetAverageServiceTimeEndpoint.cs`:

```csharp
using GarageFlow.Application.WorkOrders.GetAverageServiceTime;
using Mediator;

namespace GarageFlow.Api.WorkOrders.GetAverageServiceTime;

public static class GetAverageServiceTimeEndpoint
{
    public static IEndpointRouteBuilder MapGetAverageServiceTimeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/work-orders/average-service-time", GetAverageServiceTime)
            .WithName("GetAverageServiceTime")
            .WithTags("Work Orders")
            .WithSummary("Get average service time for completed work orders")
            .Produces<GetAverageServiceTimeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetAverageServiceTime(
        DateTime from,
        DateTime to,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAverageServiceTimeQuery(from, to), cancellationToken);

        var response = new GetAverageServiceTimeResponse(
            From: result.From,
            To: result.To,
            CompletedWorkOrdersCount: result.CompletedWorkOrdersCount,
            AverageDurationMinutes: result.AverageDurationMinutes);

        return Results.Ok(response);
    }
}
```

Add this using to `Api/WorkOrders/WorkOrderEndpoints.cs`:

```csharp
using GarageFlow.Api.WorkOrders.GetAverageServiceTime;
```

Register the endpoint in the staff route group after `MapListWorkOrdersEndpoint()`:

```csharp
staffRoutes.MapListWorkOrdersEndpoint();
staffRoutes.MapGetAverageServiceTimeEndpoint();
staffRoutes.MapGetWorkOrderByIdEndpoint();
```

In `WorkOrderEntityConfiguration.cs`, add these property mappings after `Status`:

```csharp
builder.Property(workOrder => workOrder.StartedAt);

builder.Property(workOrder => workOrder.CompletedAt);
```

Add this index near the existing work-order indexes:

```csharp
builder.HasIndex(workOrder => workOrder.CompletedAt);
```

- [ ] **Step 4: Run focused integration and architecture tests**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Integration.Api.WorkOrders.WorkOrdersApiTests.AverageServiceTime" -- RunConfiguration.MaxCpuCount=1
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~Architecture"
```

Expected: PASS. The architecture test confirms the new endpoint depends on Application and does not reference Domain, Infrastructure, or BuildingBlocks.

- [ ] **Step 5: Commit API and integration coverage**

Run:

```bash
git add Api/WorkOrders/GetAverageServiceTime Api/WorkOrders/WorkOrderEndpoints.cs Infrastructure/WorkOrders/Configurations/WorkOrderEntityConfiguration.cs Tests/Integration/Api/WorkOrders/Contracts/AverageServiceTimeResponse.cs Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs
git commit -m "feat: expose average service time endpoint"
```

---

### Task 4: Full CI-Aligned Verification

**Files:**
- Verify: whole solution

- [ ] **Step 1: Run build with warnings treated as errors**

Run:

```bash
dotnet build GarageFlow.slnx -warnaserror
```

Expected: PASS with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 2: Run unit tests**

Run:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/Unit
```

Expected: PASS.

- [ ] **Step 3: Run integration tests**

Run:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/Integration -- RunConfiguration.MaxCpuCount=1
```

Expected: PASS.

- [ ] **Step 4: Run full solution tests**

Run:

```bash
dotnet test GarageFlow.slnx --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults/Solution -- RunConfiguration.MaxCpuCount=1
```

Expected: PASS.

- [ ] **Step 5: Validate local coverage is at least 80%**

Run this PowerShell script from the repository root:

```powershell
$threshold = 80.0
$reportFiles = Get-ChildItem -Path TestResults -Filter coverage.opencover.xml -Recurse

if ($reportFiles.Count -eq 0) {
    throw "No OpenCover reports found under TestResults/**/coverage.opencover.xml"
}

$coveredPoints = [System.Collections.Generic.HashSet[string]]::new()
$totalPoints = [System.Collections.Generic.HashSet[string]]::new()

foreach ($reportFile in $reportFiles) {
    [xml]$report = Get-Content -LiteralPath $reportFile.FullName
    $files = @{}

    foreach ($fileNode in $report.SelectNodes("//Module/Files/File")) {
        $files[$fileNode.uid] = $fileNode.fullPath
    }

    foreach ($sequencePoint in $report.SelectNodes("//SequencePoint")) {
        $fileId = $sequencePoint.fileid
        $filePath = if ($files.ContainsKey($fileId)) { $files[$fileId] } else { $fileId }
        $key = "$filePath|$($sequencePoint.sl)|$($sequencePoint.sc)|$($sequencePoint.el)|$($sequencePoint.ec)"
        [void]$totalPoints.Add($key)

        if ([int]$sequencePoint.vc -gt 0) {
            [void]$coveredPoints.Add($key)
        }
    }
}

$total = $totalPoints.Count
$covered = $coveredPoints.Count
$coverage = if ($total -eq 0) { 0.0 } else { ($covered / $total) * 100.0 }

Write-Host ("Coverage total: {0:N2}% ({1}/{2} sequence points)" -f $coverage, $covered, $total)

if ($coverage -lt $threshold) {
    throw ("Coverage {0:N2}% is below threshold {1:N2}%" -f $coverage, $threshold)
}
```

Expected: script prints coverage at or above `80.00%`.

- [ ] **Step 6: Review duplication risk before final handoff**

Run:

```bash
git diff --stat HEAD~3..HEAD
git diff HEAD~3..HEAD -- Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs
```

Expected: repeated setup in tests is factored into the helper methods added in this plan: `CreateAverageServiceTimeUrl`, `FormatUtc`, `CreateServiceAsync`, and the extended `CreateWorkOrderRepositoryMock`. If the diff shows copied authentication or URL construction blocks, collapse them into those helpers before requesting review so Sonar duplication remains below `3%`.

- [ ] **Step 7: Check working tree**

Run:

```bash
git status --short
```

Expected: no unstaged or uncommitted changes.
