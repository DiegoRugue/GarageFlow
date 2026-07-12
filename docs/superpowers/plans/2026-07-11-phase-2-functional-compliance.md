# GarageFlow Phase 2 Functional Compliance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the exact Phase 2 work-order lifecycle, preserve the current ID-based opening endpoint, add the atomic and idempotent complete-intake endpoint, expose a focused status query, and turn the staff list into the required active operational queue.

**Architecture:** Keep invariants in Domain, orchestration and canonical input handling in Application, HTTP mapping in Adapters.Api, and EF Core/idempotency/query translation in Adapters.Infrastructure. All mutations continue through `TransactionBehavior`; the complete-intake request claims its idempotency key inside that transaction before creating any aggregate.

**Tech Stack:** C#/.NET SDK 10.0.301 (`net10.0`), ASP.NET Core Minimal APIs, Mediator 3, EF Core 10, Npgsql/PostgreSQL 17, EF InMemory, xUnit, Moq, Testcontainers.

## Global Constraints

- Preserve the dependency direction and vertical-slice layout defined in `AGENTS.md`; `Host` remains the only composition root and executable.
- Keep `POST /work-orders` staff-authorized and ID-based; add a separate staff-authorized `POST /work-orders/intake`.
- Persist work-order statuses as strings and use exactly `Received`, `Diagnosing`, `WaitingApproval`, `InProgress`, `Completed`, `Delivered`, and exceptional `Cancelled`.
- Remove the work-order status `Approved` and the redundant `StartWork` API/use case; estimate status `Approved` remains valid.
- Approval moves `WaitingApproval` directly to `InProgress`; rejection moves it back to `Diagnosing`.
- Release outstanding inventory reservations exactly once on estimate rejection or work-order cancellation, in the same application transaction.
- Require at least one service in complete intake; inventory items are optional; reject duplicate service descriptions and duplicate inventory item names case-insensitively after value-object normalization.
- Treat inventory item `type` as an API/Application string (`Part` or `Supply`), never as a numeric HTTP contract.
- Treat an existing customer tax document or vehicle plate as `409 Conflict`; only brand, model, and color may be resolved and reused by normalized name.
- Idempotency semantics are exact: first request `201`, same `requestId` and payload `200`, same `requestId` and different payload `409`, and concurrent equal requests create one graph.
- `GET /work-orders` lists active staff work only in priority `InProgress`, `WaitingApproval`, `Diagnosing`, `Received`, then oldest `CreatedAt`, then `Id`; it excludes all terminal statuses.
- Preserve the authenticated customer's complete historical list and owned detail route; only the staff list becomes active-only.
- Run every mutating use case through `ICommand<T>` and `TransactionBehavior`; handlers must not begin, save, commit, or roll back transactions.
- Use synthetic data in all tests and examples.

---

## File Structure

### Create

- `Application/InventoryItems/Common/InventoryItemTypeParser.cs` — one parser shared by inventory CRUD and complete intake.
- `Application/Common/Messaging/ICorrelatedCommand.cs` — stable correlation supplied by intake and, later, webhook commands.
- `Domain/WorkOrders/ValueObjects/InventoryReservation.cs` — immutable item/quantity release instruction returned by the aggregate.
- `Application/WorkOrders/Common/EstimateDecisionProcessor.cs` — shared approval/rejection and stock-release orchestration, later reused by the signed webhook.
- `Application/WorkOrders/Ports/IWorkOrderIntakeRequestStore.cs` — idempotency claim/completion port.
- `Application/WorkOrders/ReadModels/IntakeRequestClaim.cs` — infrastructure-neutral claim result.
- `Application/WorkOrders/ReadModels/IntakeRequestClaimState.cs` — `Acquired` or `Completed`.
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/CreateWorkOrderIntakeCommand.cs` — complete intake command.
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/IntakeCustomerInput.cs`
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/IntakeVehicleInput.cs`
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/IntakeServiceInput.cs`
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/IntakeInventoryItemInput.cs`
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/CreateWorkOrderIntakeResult.cs`
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/StoredWorkOrderIntakeResponse.cs`
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/IntakePayloadCanonicalizer.cs`
- `Application/WorkOrders/UseCases/CreateWorkOrderIntake/CreateWorkOrderIntakeHandler.cs`
- `Adapters.Api/WorkOrders/CreateWorkOrderIntake/CreateWorkOrderIntakeEndpoint.cs`
- `Adapters.Api/WorkOrders/CreateWorkOrderIntake/CreateWorkOrderIntakeRequest.cs`
- `Adapters.Api/WorkOrders/CreateWorkOrderIntake/IntakeCustomerRequest.cs`
- `Adapters.Api/WorkOrders/CreateWorkOrderIntake/IntakeVehicleRequest.cs`
- `Adapters.Api/WorkOrders/CreateWorkOrderIntake/IntakeServiceRequest.cs`
- `Adapters.Api/WorkOrders/CreateWorkOrderIntake/IntakeInventoryItemRequest.cs`
- `Adapters.Api/WorkOrders/CreateWorkOrderIntake/CreateWorkOrderIntakeResponse.cs`
- `Adapters.Infrastructure/WorkOrders/Idempotency/IntakeRequestReceipt.cs`
- `Adapters.Infrastructure/WorkOrders/Idempotency/IntakeRequestReceiptEntityConfiguration.cs`
- `Adapters.Infrastructure/WorkOrders/Idempotency/EfWorkOrderIntakeRequestStore.cs`
- `Application/WorkOrders/ReadModels/WorkOrderStatusReadModel.cs`
- `Application/WorkOrders/UseCases/GetWorkOrderStatus/GetWorkOrderStatusQuery.cs`
- `Application/WorkOrders/UseCases/GetWorkOrderStatus/GetWorkOrderStatusHandler.cs`
- `Application/WorkOrders/UseCases/GetWorkOrderStatus/GetWorkOrderStatusResult.cs`
- `Adapters.Api/WorkOrders/GetWorkOrderStatus/GetWorkOrderStatusEndpoint.cs`
- `Adapters.Api/WorkOrders/GetWorkOrderStatus/GetWorkOrderStatusResponse.cs`
- `Tests/Shared/WorkOrders/CreateWorkOrderIntakeRequest.cs`
- `Tests/Shared/WorkOrders/IntakeCustomerRequest.cs`
- `Tests/Shared/WorkOrders/IntakeVehicleRequest.cs`
- `Tests/Shared/WorkOrders/IntakeServiceRequest.cs`
- `Tests/Shared/WorkOrders/IntakeInventoryItemRequest.cs`
- `Tests/Shared/WorkOrders/CreateWorkOrderIntakeResponse.cs`
- `Tests/Unit/WorkOrders/CreateWorkOrderIntakeHandlerTests.cs`
- `Tests/Unit/WorkOrders/EstimateDecisionProcessorTests.cs`
- `Tests/Integration/Api/WorkOrders/Contracts/WorkOrderStatusResponse.cs`
- `Tests/E2E/WorkOrders/Phase2StatusMigrationE2eTests.cs`
- `Adapters.Infrastructure/DataAccess/Migrations/20260711090000_WorkOrderFunctionalCompliance.cs` and `.Designer.cs` — schema plus status-data migration.

### Modify

- Inventory string contract: every production and test file returned by `rg -n "\\bint Type\\b|Type: \\(int\\)" Adapters.Api Application Adapters.Infrastructure Tests`.
- `SharedKernel/Domain/ValueObjects/PhoneNumber.cs` and its unit tests — accept optional Brazilian country code `+55`, persist the existing 10/11-digit national form.
- `Domain/WorkOrders/Enums/WorkOrderStatus.cs`, `Domain/WorkOrders/Entities/WorkOrder.cs`, and `Tests/Unit/WorkOrders/WorkOrderTests.cs`.
- `Application/WorkOrders/UseCases/ApproveMyEstimate/ApproveMyEstimateHandler.cs`.
- `Application/WorkOrders/UseCases/RejectMyEstimate/RejectMyEstimateHandler.cs`.
- `Application/WorkOrders/UseCases/CancelWorkOrder/CancelWorkOrderHandler.cs`.
- `Application/WorkOrders/UseCases/StartEstimateService/StartEstimateServiceHandler.cs` only if its constructor or assertions reference `StartWork`; the domain call inside `WorkOrder.StartEstimateService` must be simplified.
- `Application/WorkOrders/Ports/IWorkOrderQueries.cs` and `Adapters.Infrastructure/WorkOrders/Repositories/EfWorkOrderQueries.cs`.
- Vehicle reference ports and repositories: `IVehicleBrandRepository`, `IVehicleModelRepository`, `IVehicleColorRepository` and their three EF implementations.
- Vehicle reference configurations — retain ordinary indexes and add case-insensitive functional unique indexes in the migration.
- `Adapters.Infrastructure/DataAccess/GarageFlowDbContext.cs` and `Adapters.Infrastructure/DataAccess/DependencyInjection.cs`.
- `Adapters.Api/WorkOrders/WorkOrderEndpoints.cs`.
- Work-order unit, integration, repository-query, shared-builder, and E2E tests.

### Delete

- `Adapters.Api/WorkOrders/StartWork/StartWorkEndpoint.cs`.
- `Application/WorkOrders/UseCases/StartWork/StartWorkCommand.cs`.
- `Application/WorkOrders/UseCases/StartWork/StartWorkHandler.cs`.

---

### Task 1: Normalize public primitive contracts

**Files:**
- Create: `Application/InventoryItems/Common/InventoryItemTypeParser.cs`
- Modify: inventory API requests/responses, Application commands/results/read models/handlers, EF query projection, shared test contracts/builders, and InventoryItems Unit/Integration/E2E tests found by the `rg` command in File Structure.
- Modify: `SharedKernel/Domain/ValueObjects/PhoneNumber.cs`
- Test: `Tests/Unit/Customers/CustomerTests.cs`
- Test: `Tests/Unit/InventoryItems/InventoryItemHandlersTests.cs`
- Test: `Tests/Integration/Api/InventoryItems/InventoryItemsApiTests.cs`

**Interfaces:**
- Produces: `InventoryItemTypeParser.Parse(string value) : InventoryItemType`.
- Produces: all API/Application inventory `Type` properties as `string`; Domain and EF persistence remain `InventoryItemType`.
- Produces: `PhoneNumber.Create("+5511999999999").Value == "11999999999"`.

- [ ] **Step 1: Write failing parser and phone-normalization tests**

Add focused assertions equivalent to:

```csharp
[Theory]
[InlineData("Part", InventoryItemType.Part)]
[InlineData("part", InventoryItemType.Part)]
[InlineData("Supply", InventoryItemType.Supply)]
public void Parse_ShouldReturnDomainType_WhenStringIsValid(string value, InventoryItemType expected)
{
    Assert.Equal(expected, InventoryItemTypeParser.Parse(value));
}

[Fact]
public void Parse_ShouldThrowValidationException_WhenStringIsUnknown()
{
    var exception = Assert.Throws<ValidationException>(() => InventoryItemTypeParser.Parse("Fluid"));
    Assert.Equal("Inventory item type 'Fluid' is invalid. Allowed values: Part, Supply.", exception.Message);
}

[Fact]
public void Parse_ShouldRejectNumericEnumRepresentation()
{
    Assert.Throws<ValidationException>(() => InventoryItemTypeParser.Parse("0"));
}

[Fact]
public void Create_ShouldRemoveBrazilCountryCode_WhenPhoneStartsWithPlus55()
{
    Assert.Equal("11999999999", PhoneNumber.Create("+55 11 99999-9999").Value);
}
```

- [ ] **Step 2: Run the focused tests and confirm the contract is still numeric**

Run:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~InventoryItemHandlersTests|FullyQualifiedName~CustomerTests"
```

Expected: FAIL because `InventoryItemTypeParser` does not exist and `+55` currently leaves 13 digits.

- [ ] **Step 3: Add the single inventory type parser**

Create `Application/InventoryItems/Common/InventoryItemTypeParser.cs`:

```csharp
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Application.InventoryItems.Common;

public static class InventoryItemTypeParser
{
    public static InventoryItemType Parse(string value)
    {
        var normalized = value?.Trim();
        var canonicalName = Enum.GetNames<InventoryItemType>()
            .SingleOrDefault(name => string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase));
        if (canonicalName is not null)
        {
            return Enum.Parse<InventoryItemType>(canonicalName);
        }

        throw new ValidationException(
            $"Inventory item type '{value}' is invalid. Allowed values: Part, Supply.");
    }
}
```

Use it in create/update handlers. Change command, result, DTO, read-model, request, response, test-contract, and builder fields from `int Type` to `string Type`; return `inventoryItem.Type.ToString()` and project `inventoryItem.Type.ToString()` in `EfInventoryItemQueries`.

- [ ] **Step 4: Normalize an optional `55` country code without changing stored format**

In `PhoneNumber.Create`, after stripping punctuation and before length validation, add:

```csharp
if (digits.Length is 12 or 13 && digits.StartsWith("55", StringComparison.Ordinal))
{
    digits = digits[2..];
}
```

Keep the existing 10/11-digit rule after that transformation.

- [ ] **Step 5: Run unit and HTTP contract tests**

Run:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~InventoryItems|FullyQualifiedName~Customers"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~InventoryItemsApiTests"
```

Expected: PASS; serialized inventory payloads contain `"type":"Part"` or `"type":"Supply"`.

- [ ] **Step 6: Commit the primitive contract alignment**

```powershell
git add Application/InventoryItems Adapters.Api/InventoryItems Adapters.Infrastructure/InventoryItems SharedKernel/Domain/ValueObjects/PhoneNumber.cs Tests
git commit -m "refactor(inventory): expose item type as string"
```

---

### Task 2: Replace the work-order lifecycle and remove StartWork

**Files:**
- Modify: `Domain/WorkOrders/Enums/WorkOrderStatus.cs`
- Modify: `Domain/WorkOrders/Entities/WorkOrder.cs`
- Delete: the three StartWork files listed above
- Modify: `Adapters.Api/WorkOrders/WorkOrderEndpoints.cs`
- Modify: status expectations in WorkOrders Unit/Integration/E2E tests and test builders

**Interfaces:**
- Produces: `WorkOrderStatus.Received = 1`, `Diagnosing = 2`, `WaitingApproval = 3`, `InProgress = 5`, `Completed = 6`, `Delivered = 7`, `Cancelled = 8`; the removed value `4` stays unused to preserve numeric compatibility.
- Produces: `ApproveEstimate(EstimateId)` sets `StartedAt` and transitions directly to `InProgress`.
- Produces: `RejectEstimate(EstimateId)` transitions to `Diagnosing` and returns reservations in Task 3.

- [ ] **Step 1: Replace lifecycle tests with the exact transition matrix**

Cover these allowed paths and reject every skipped or backward transition:

```text
Received -> Diagnosing
Diagnosing -> WaitingApproval
WaitingApproval -> InProgress       (approval)
WaitingApproval -> Diagnosing       (rejection)
InProgress -> Completed
Completed -> Delivered
Received|Diagnosing|WaitingApproval|InProgress -> Cancelled
```

Assert every actual status change updates `UpdatedAt` and adds one `WorkOrderStatusChanged` with exact previous/new status. Assert approval sets `StartedAt`, and assert a newly created work order reports `Received`.

- [ ] **Step 2: Run WorkOrder domain tests and observe old statuses**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrderTests"
```

Expected: FAIL on `Created`, intermediate `Approved`, rejection remaining `WaitingApproval`, and the old StartWork flow.

- [ ] **Step 3: Implement the new enum and transitions**

Use this enum exactly:

```csharp
public enum WorkOrderStatus
{
    Received = 1,
    Diagnosing = 2,
    WaitingApproval = 3,
    InProgress = 5,
    Completed = 6,
    Delivered = 7,
    Cancelled = 8
}
```

In `WorkOrder`:

- initialize `Status = WorkOrderStatus.Received`;
- allow estimate submission from `Received`, `Diagnosing`, or `WaitingApproval`;
- make first submission advance `Received -> Diagnosing -> WaitingApproval`;
- make approval call `TransitionTo(InProgress, approvedAt)` and set `StartedAt = approvedAt`;
- make rejection transition `WaitingApproval -> Diagnosing` after `Estimate.Reject()`;
- remove `StartWork()` and the `Approved` branch from `StartEstimateService()`; service start now requires `InProgress`;
- allow cancellation from `InProgress` and remove all `Approved` branches.

- [ ] **Step 4: Remove the redundant API and application slice**

Delete the three StartWork files, its using directive, and `staffRoutes.MapStartWorkEndpoint()` from `WorkOrderEndpoints`. Update tests so `/work-orders/{id}/start-work` is no longer an advertised route and approval is the only transition into execution.

- [ ] **Step 5: Run domain, handler, and API tests**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrders"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~WorkOrdersApiTests"
```

Expected: PASS with no `WorkOrderStatus.Created`, `WorkOrderStatus.Approved`, `StartWorkCommand`, or `MapStartWorkEndpoint` references.

- [ ] **Step 6: Prove obsolete symbols are gone and commit**

```powershell
if (rg -n "WorkOrderStatus\.(Created|Approved)|StartWork" Domain Application Adapters.Api Tests) { exit 1 }
git add Domain/WorkOrders Application/WorkOrders Adapters.Api/WorkOrders Tests
git commit -m "refactor(work-orders): align phase 2 lifecycle"
```

---

### Task 3: Release inventory reservations exactly once

**Files:**
- Create: `Domain/WorkOrders/ValueObjects/InventoryReservation.cs`
- Create: `Application/WorkOrders/Common/EstimateDecisionProcessor.cs`
- Create: `Tests/Unit/WorkOrders/EstimateDecisionProcessorTests.cs`
- Modify: `Domain/WorkOrders/Entities/WorkOrder.cs`
- Modify: approve, reject, and cancel handlers
- Modify: `Tests/Unit/WorkOrders/WorkOrderTests.cs`
- Modify: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`

**Interfaces:**
- Produces: `WorkOrder.RejectEstimate(EstimateId) : IReadOnlyList<InventoryReservation>`.
- Produces: `WorkOrder.Cancel() : IReadOnlyList<InventoryReservation>`; an already-cancelled order returns an empty list.
- Produces: `EstimateDecisionProcessor.Approve(WorkOrder, EstimateId)` and `RejectAsync(WorkOrder, EstimateId, CancellationToken)`.
- Produces: `EstimateDecisionProcessor.ReleaseAsync(IReadOnlyCollection<InventoryReservation>, CancellationToken)` for cancellation reuse.

- [ ] **Step 1: Write domain and processor failure tests**

The tests must prove:

```csharp
Assert.Equal(WorkOrderStatus.Diagnosing, rejectedWorkOrder.Status);
Assert.Collection(releases, release =>
{
    Assert.Equal(inventoryItemId, release.InventoryItemId);
    Assert.Equal(2, release.Quantity.Value);
});

var firstCancellation = workOrder.Cancel();
var repeatedCancellation = workOrder.Cancel();
Assert.NotEmpty(firstCancellation);
Assert.Empty(repeatedCancellation);
```

Also assert cancellation releases lines from Draft, Pending, and Approved estimates—including cancellation from `InProgress`—but never releases Rejected lines that were already restored. Group repeated item IDs and process inventory IDs in deterministic order.

- [ ] **Step 2: Run the focused tests and confirm cancellation does not restore stock**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrderTests|FullyQualifiedName~EstimateDecisionProcessorTests"
```

Expected: FAIL because `InventoryReservation` and the processor do not exist and `CancelWorkOrderHandler` has no inventory repository.

- [ ] **Step 3: Add the domain reservation value and return release instructions**

Create:

```csharp
public sealed record InventoryReservation(
    InventoryItemId InventoryItemId,
    EstimateItemQuantity Quantity);
```

`RejectEstimate` must reject the pending estimate, transition the work order back to `Diagnosing`, and return that estimate's inventory lines grouped by `InventoryItemId`. `Cancel` must first return `[]` when already cancelled; otherwise collect Draft/Pending/Approved estimate lines, exclude Rejected lines already restored, transition once, and return grouped reservations. The aggregate never loads or mutates `InventoryItem`.

- [ ] **Step 4: Add one application processor for approval, rejection, and release**

Its public surface is:

```csharp
public sealed class EstimateDecisionProcessor(IInventoryItemRepository inventoryItemRepository)
{
    public void Approve(WorkOrder workOrder, EstimateId estimateId) =>
        workOrder.ApproveEstimate(estimateId);

    public Task RejectAsync(
        WorkOrder workOrder,
        EstimateId estimateId,
        CancellationToken cancellationToken) =>
        ReleaseAsync(workOrder.RejectEstimate(estimateId), cancellationToken);

    public async Task ReleaseAsync(
        IReadOnlyCollection<InventoryReservation> reservations,
        CancellationToken cancellationToken)
    {
        foreach (var reservation in reservations.OrderBy(item => item.InventoryItemId.Value))
        {
            var item = await inventoryItemRepository.GetByIdForStockReservationAsync(
                reservation.InventoryItemId,
                cancellationToken);
            if (item is null)
            {
                throw new NotFoundException(
                    $"Inventory item with ID '{reservation.InventoryItemId.Value}' was not found.");
            }

            item.IncreaseStock(reservation.Quantity.Value);
        }
    }
}
```

Register the concrete processor as scoped in `Host/Program.cs`; this is application orchestration, not an adapter port.

- [ ] **Step 5: Refactor handlers and lock cancellation mutations**

- `ApproveMyEstimateHandler` calls `processor.Approve(...)`.
- `RejectMyEstimateHandler` calls `await processor.RejectAsync(...)` and removes its duplicate loop.
- `CancelWorkOrderHandler` injects the processor, loads with `GetByIdForEstimateMutationAsync`, calls `workOrder.Cancel()`, then `ReleaseAsync`.

- [ ] **Step 6: Run unit and integration stock tests**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrders"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~WorkOrders"
```

Expected: PASS; rejection restores its pending estimate once, cancellation restores every still-reserved Draft/Pending/Approved line once, and approval alone does not restore stock.

- [ ] **Step 7: Commit reservation consistency**

```powershell
git add Domain/WorkOrders Application/WorkOrders Host/Program.cs Tests
git commit -m "fix(work-orders): restore reserved stock exactly once"
```

---

### Task 4: Add atomic complete intake and idempotency storage

**Files:**
- Create: all Application complete-intake and idempotency files listed in File Structure
- Create: all Infrastructure idempotency files listed in File Structure
- Modify: customer, vehicle-reference, vehicle, service, inventory, and work-order ports/repositories
- Modify: `GarageFlowDbContext.cs` and Infrastructure dependency registration
- Test: `Tests/Unit/WorkOrders/CreateWorkOrderIntakeHandlerTests.cs`
- Test: `Tests/Integration/WorkOrders/WorkOrderRepositoryQueryTests.cs`

**Interfaces:**
- Consumes: `InventoryItemTypeParser`, existing domain factories, repository ports, and `ICommand<CreateWorkOrderIntakeResult>`.
- Produces: the complete command/result contracts below.
- Produces: atomic `ClaimAsync`/`CompleteAsync` idempotency storage.
- Produces: case-insensitive `GetByNameAsync` methods for brand, model-within-brand, and color.

- [ ] **Step 1: Add failing handler tests for every intake outcome**

Cover at least:

- complete success with one service and no inventory;
- complete success with multiple services/items and stock reservation;
- zero services, duplicate normalized service description, duplicate normalized inventory name;
- existing tax document, existing plate, insufficient stock;
- reference reuse by case-insensitive brand/model/color name;
- any repository/domain failure leaves no completed receipt;
- acquired claim produces a new graph;
- completed equal claim returns the stored response with `IsReplay=true`;
- completed different-hash claim throws `BusinessRuleViolationException`.

Instantiate the handler with strict Moq repositories so an unexpected creation or second stock mutation fails the test.

- [ ] **Step 2: Define immutable command and result contracts**

Use one public type per file and these exact shapes:

```csharp
public sealed record CreateWorkOrderIntakeCommand(
    Guid RequestId,
    IntakeCustomerInput? Customer,
    IntakeVehicleInput? Vehicle,
    IReadOnlyList<IntakeServiceInput>? Services,
    IReadOnlyList<IntakeInventoryItemInput>? InventoryItems)
    : ICommand<CreateWorkOrderIntakeResult>, ICorrelatedCommand
{
    public string CorrelationId => RequestId.ToString("D");
}

public sealed record IntakeCustomerInput(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber);

public sealed record IntakeVehicleInput(
    string Plate,
    int Year,
    string Brand,
    string Model,
    string Color);

public sealed record IntakeServiceInput(string Description, decimal Price);

public sealed record IntakeInventoryItemInput(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    int Quantity);

public sealed record CreateWorkOrderIntakeResult(
    bool IsReplay,
    Guid WorkOrderId,
    Guid CustomerId,
    Guid VehicleId,
    Guid EstimateId,
    IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> InventoryItemIds,
    string Status,
    DateTime CreatedAt);
```

`StoredWorkOrderIntakeResponse` has the same fields except `IsReplay`.

- [ ] **Step 3: Define the idempotency port and claim contract**

Create the shared correlation marker first:

```csharp
namespace GarageFlow.Application.Common.Messaging;

public interface ICorrelatedCommand
{
    string CorrelationId { get; }
}
```

Then add the intake claim types:

```csharp
public enum IntakeRequestClaimState
{
    Acquired = 1,
    Completed = 2
}

public sealed record IntakeRequestClaim(
    IntakeRequestClaimState State,
    string PayloadHash,
    string? ResponseJson);

public interface IWorkOrderIntakeRequestStore
{
    Task<IntakeRequestClaim> ClaimAsync(
        Guid requestId,
        string payloadHash,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid requestId,
        Guid workOrderId,
        string responseJson,
        DateTime completedAt,
        CancellationToken cancellationToken = default);
}
```

The Application compares the stored hash and deserializes only a completed equal claim. Infrastructure never decides whether two business payloads are equivalent.

- [ ] **Step 4: Add normalized reference lookup ports**

Add these signatures and implement case-insensitive tracked lookups for both Npgsql and InMemory:

```csharp
Task<VehicleBrand?> GetByNameAsync(VehicleBrandName name, CancellationToken cancellationToken = default);
Task<VehicleModel?> GetByNameAsync(VehicleBrandId brandId, VehicleModelName name, CancellationToken cancellationToken = default);
Task<VehicleColor?> GetByNameAsync(VehicleColorName name, CancellationToken cancellationToken = default);
```

For PostgreSQL use parameterized `FromSqlInterpolated` with `upper("Name") = upper(...)`; for InMemory use `StringComparison.OrdinalIgnoreCase`. Return tracked entities because newly created references and the vehicle are saved in one unit of work.

Preserve duplicate-natural-key HTTP semantics at the commit boundary: in `GarageFlowDbContext.SaveChangesAsync`, catch only a `DbUpdateException` whose inner `PostgresException.SqlState` is `PostgresErrorCodes.UniqueViolation`, then throw `BusinessRuleViolationException("A resource with the same unique value already exists.")`. Do not expose constraint names or database detail. The integration-reliability plan must preserve this translation when it adds domain-event staging.

- [ ] **Step 5: Implement a canonical SHA-256 hash after value-object validation**

`IntakePayloadCanonicalizer.Compute(...)` must serialize a fixed property order containing normalized tax document, contact values, plate, year, reference names, services, and inventory inputs; exclude `requestId`; preserve list order; encode enum names through `InventoryItemTypeParser`; then return lowercase SHA-256:

```csharp
var json = JsonSerializer.SerializeToUtf8Bytes(canonicalPayload, JsonOptions);
return Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
```

The canonical payload must use `.Value` from the constructed value objects, decimal values unchanged, and `StringComparer.OrdinalIgnoreCase` only for duplicate detection—not for hashing the user-visible values.

- [ ] **Step 6: Implement the PostgreSQL-first claim store**

Map `IntakeRequestReceipt` to table `WorkOrderIntakeRequests` with:

```text
RequestId uuid primary key
PayloadHash varchar(64) not null
WorkOrderId uuid null
ResponseJson jsonb null
CreatedAt timestamp with time zone not null
CompletedAt timestamp with time zone null
```

`WorkOrderId` is an indexed logical identifier without a foreign key: the receipt is completed before the tracked `WorkOrder` insert is flushed, so an immediate FK check would reject the otherwise atomic transaction.

For Npgsql, `ClaimAsync` executes this inside the transaction already opened by `TransactionBehavior`:

```sql
INSERT INTO "WorkOrderIntakeRequests" ("RequestId", "PayloadHash", "CreatedAt")
VALUES (@requestId, @payloadHash, CURRENT_TIMESTAMP)
ON CONFLICT ("RequestId") DO NOTHING;
```

If one row was inserted, return `Acquired`. Otherwise load the committed row and return `Completed`; under the default PostgreSQL `READ COMMITTED` isolation, a concurrent equal request blocks on the unique index until the winner commits, and a rolled-back winner leaves no row so the waiting insert can acquire. Keep that isolation level. The InMemory branch performs tracked lookup/add for sequential integration tests and is not used to claim concurrency coverage.

For Npgsql, `CompleteAsync` performs a parameterized raw `UPDATE` of `WorkOrderId`, `CAST(@responseJson AS jsonb)`, and `CompletedAt`, asserts exactly one affected row, and never calls `SaveChangesAsync`. This avoids relying on tracking for the row inserted by raw SQL. The subsequent transaction commit flushes the aggregate graph and commits both operations together.

- [ ] **Step 7: Implement the handler orchestration in the required order**

The handler performs exactly:

1. reject `Guid.Empty` request ID and null collections;
2. construct every value object and parse every item type/quantity;
3. require at least one service and reject normalized duplicates;
4. compute the canonical hash and call `ClaimAsync`;
5. return stored response for equal completed claims, or throw conflict for hash mismatch;
6. reject existing customer tax document and vehicle plate;
7. resolve-or-create brand, model under brand, and color;
8. create/add customer and vehicle;
9. create/add each `Service` and `InventoryItem`, decreasing each new item's stock by reserved quantity;
10. create/add `WorkOrder`, call `CreateEstimate`, add snapshot service/inventory lines;
11. serialize `StoredWorkOrderIntakeResponse`, call `CompleteAsync`, and return `IsReplay=false`.

No handler save/transaction calls are allowed. Any exception bubbles through the existing typed exception middleware and `TransactionBehavior` clears/rolls back the entire graph and the idempotency claim.

- [ ] **Step 8: Run intake unit and repository tests**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~CreateWorkOrderIntakeHandlerTests"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~WorkOrders"
```

Expected: PASS for all handler paths and InMemory claim behavior.

- [ ] **Step 9: Commit the application and persistence core**

```powershell
git add Application Domain Adapters.Infrastructure Tests/Unit Tests/Integration
git commit -m "feat(work-orders): add atomic complete intake"
```

---

### Task 5: Expose the complete-intake HTTP endpoint

**Files:**
- Create: all `Adapters.Api/WorkOrders/CreateWorkOrderIntake/*` files
- Create: all `Tests/Shared/WorkOrders/Intake*` contracts and response
- Modify: `Adapters.Api/WorkOrders/WorkOrderEndpoints.cs`
- Modify: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`
- Modify: `Tests/E2E/WorkOrders/WorkOrdersE2eTests.cs`

**Interfaces:**
- Consumes: `CreateWorkOrderIntakeCommand` and result from Task 4.
- Produces: `POST /work-orders/intake` with `201/200/400/401/403/409/500` metadata.

- [ ] **Step 1: Write unauthorized, success, replay, and conflict API tests**

Use the representative payload from the approved specification, but replace its illustrative invalid CPF with the valid synthetic CPF `52998224725`; keep `+5511999999999` and `type: "Part"`. Never weaken `TaxDocument` check-digit validation. Assert:

```text
no JWT -> 401
active customer JWT -> 403
active staff, first request -> 201 + Location /work-orders/{id}
same requestId and body -> 200 + byte-equivalent business fields
same requestId and changed service price -> 409 ProblemDetails
existing tax document or plate -> 409 ProblemDetails
zero services or insufficient stock -> 400/409 as specified
```

- [ ] **Step 2: Create API-owned nested records**

Mirror the command inputs with API records and keep `Type` as `string`. `CreateWorkOrderIntakeRequest` is:

```csharp
public sealed record CreateWorkOrderIntakeRequest(
    Guid RequestId,
    IntakeCustomerRequest? Customer,
    IntakeVehicleRequest? Vehicle,
    IReadOnlyList<IntakeServiceRequest>? Services,
    IReadOnlyList<IntakeInventoryItemRequest>? InventoryItems);
```

The response fields exactly match the approved JSON and do not expose `IsReplay`. Declare `Customer`, `Vehicle`, `Services`, and `InventoryItems` nullable on the API request as well; the handler owns the explicit required/null validation so missing JSON members become typed `400` responses.

- [ ] **Step 3: Implement the thin endpoint and status selection**

Map request to command, invoke Mediator, map the response once, then:

```csharp
return result.IsReplay
    ? Results.Ok(response)
    : Results.Created($"/work-orders/{result.WorkOrderId}", response);
```

Register it on the existing `ActiveStaff` route group immediately after `MapCreateWorkOrderEndpoint()`.

- [ ] **Step 4: Run HTTP and PostgreSQL concurrency tests**

In E2E, send two concurrent identical requests with one `HttpClient` per task. Assert one status is `Created`, one is `OK`, both return the same IDs, and database-backed detail/list results contain one work order/customer/vehicle graph. Then reuse the ID with a changed body and assert `409`.

```powershell
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~WorkOrdersApiTests"
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~WorkOrdersE2eTests"
```

Expected: PASS with Docker running.

- [ ] **Step 5: Commit the HTTP slice**

```powershell
git add Adapters.Api/WorkOrders Tests/Shared/WorkOrders Tests/Integration/Api/WorkOrders Tests/E2E/WorkOrders
git commit -m "feat(api): expose complete work-order intake"
```

---

### Task 6: Add the focused status query and active staff queue

**Files:**
- Create: status read model/query/result/handler/endpoint/response files listed in File Structure
- Modify: `Application/WorkOrders/Ports/IWorkOrderQueries.cs`
- Modify: `Application/WorkOrders/UseCases/ListWorkOrders/ListWorkOrdersHandler.cs`
- Modify: `Adapters.Infrastructure/WorkOrders/Repositories/EfWorkOrderQueries.cs`
- Modify: `Adapters.Api/WorkOrders/WorkOrderEndpoints.cs`
- Test: WorkOrders unit, repository, API, and E2E suites

**Interfaces:**
- Produces: `GetStatusByIdAsync(WorkOrderId, CancellationToken) : Task<WorkOrderStatusReadModel?>`.
- Produces: `ListActiveDetailsAsync(int page, int pageSize, CustomerId? customerId, CancellationToken)`.
- Preserves: `ListCustomerDetailsAsync` as complete customer history.

- [ ] **Step 1: Write failing query and API tests**

Seed mixed states and timestamps. Assert staff ordering is:

```text
InProgress oldest -> newest
WaitingApproval oldest -> newest
Diagnosing oldest -> newest
Received oldest -> newest
```

Use `Id` for equal timestamps, exclude Completed/Delivered/Cancelled, include terminal orders in `GET /me/work-orders`, preserve staff customer filtering, and assert `GET /work-orders/{id}/status` returns only `id`, `status`, `updatedAt` with staff auth/404 behavior.

- [ ] **Step 2: Split active and customer-history query ports**

Add:

```csharp
Task<WorkOrderStatusReadModel?> GetStatusByIdAsync(
    WorkOrderId id,
    CancellationToken cancellationToken = default);

Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListActiveDetailsAsync(
    int page,
    int pageSize,
    CustomerId? customerId = null,
    CancellationToken cancellationToken = default);
```

Keep `ListCustomerDetailsAsync` and make it query all customer statuses directly instead of delegating to the active method.

- [ ] **Step 3: Implement one server-side CASE ordering**

The active EF query filters before counting/paging and uses a translatable expression:

```csharp
.Where(workOrder =>
    workOrder.Status == WorkOrderStatus.InProgress ||
    workOrder.Status == WorkOrderStatus.WaitingApproval ||
    workOrder.Status == WorkOrderStatus.Diagnosing ||
    workOrder.Status == WorkOrderStatus.Received)
.OrderBy(workOrder => workOrder.Status == WorkOrderStatus.InProgress ? 0
    : workOrder.Status == WorkOrderStatus.WaitingApproval ? 1
    : workOrder.Status == WorkOrderStatus.Diagnosing ? 2
    : 3)
.ThenBy(workOrder => workOrder.CreatedAt)
.ThenBy(workOrder => workOrder.Id)
```

Do not materialize before ordering or paging. Project the focused status query with `AsNoTracking()` and only the three required fields.

- [ ] **Step 4: Add the query handler and thin endpoint**

Use:

```csharp
public sealed record GetWorkOrderStatusQuery(Guid Id) : IRequest<GetWorkOrderStatusResult>;

public sealed record GetWorkOrderStatusResult(Guid Id, string Status, DateTime UpdatedAt);
```

Throw the existing exact `NotFoundException` message. Map `GET /work-orders/{id:guid}/status` under the staff group with complete metadata and response statuses.

- [ ] **Step 5: Run unit, EF translation, API, and E2E tests**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrders"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~WorkOrders"
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~WorkOrders"
```

Expected: PASS; the PostgreSQL run proves the conditional order translates and pages server-side.

- [ ] **Step 6: Commit query compliance**

```powershell
git add Application/WorkOrders Adapters.Infrastructure/WorkOrders Adapters.Api/WorkOrders Tests
git commit -m "feat(work-orders): add status and active queue queries"
```

---

### Task 7: Generate the functional migration and close the checkpoint

**Files:**
- Create: `Adapters.Infrastructure/DataAccess/Migrations/20260711090000_WorkOrderFunctionalCompliance.cs`
- Create: generated designer
- Modify: `GarageFlowDbContextModelSnapshot.cs`
- Create: `Tests/E2E/WorkOrders/Phase2StatusMigrationE2eTests.cs`
- Modify: `Adapters.Infrastructure/DataAccess/Migrations/README.md`

**Interfaces:**
- Migrates stored work-order strings `Created -> Received` and `Approved -> InProgress`.
- Creates `WorkOrderIntakeRequests` with a primary/unique `RequestId`.
- Leaves estimate status string `Approved` unchanged.

- [ ] **Step 1: Generate the migration from the final model**

```powershell
dotnet ef migrations add WorkOrderFunctionalCompliance --project Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj --startup-project Host/GarageFlow.Host.csproj
```

Rename the generated migration pair to `20260711090000_WorkOrderFunctionalCompliance.cs` and `.Designer.cs`, then set the designer's `[Migration]` value to `20260711090000_WorkOrderFunctionalCompliance`. Expected: deterministic migration identity, snapshot changes, and no legacy `Infrastructure`/`Api` project paths.

- [ ] **Step 2: Add explicit status data SQL to `Up` and safe rollback SQL to `Down`**

`Up` executes:

```sql
UPDATE "WorkOrders" SET "Status" = 'Received' WHERE "Status" = 'Created';
UPDATE "WorkOrders" SET "Status" = 'InProgress' WHERE "Status" = 'Approved';
```

Before adding reference indexes, fail migration with a clear exception if case-only duplicate brand, color, or model-within-brand rows already exist. Then add:

```sql
CREATE UNIQUE INDEX "UX_VehicleBrands_Name_CaseInsensitive"
    ON "VehicleBrands" (upper("Name"));
CREATE UNIQUE INDEX "UX_VehicleColors_Name_CaseInsensitive"
    ON "VehicleColors" (upper("Name"));
CREATE UNIQUE INDEX "UX_VehicleModels_Brand_Name_CaseInsensitive"
    ON "VehicleModels" ("VehicleBrandId", upper("Name"));
```

This keeps normalized dictionary integrity under concurrent requests; a losing natural-key insert is translated to `409`. `Down` drops those three functional indexes and maps only `Received -> Created`. It deliberately leaves `InProgress` unchanged because the old model already supports `InProgress` and cannot distinguish previously approved rows from genuinely executing rows.

- [ ] **Step 3: Test migration from the previous schema on PostgreSQL**

The E2E test creates a dedicated database, migrates to `20260502194707_EstimateServiceLineExecution`, inserts one `Created` and one `Approved` work-order row plus an estimate whose status is `Approved`, migrates to latest, then asserts:

```text
work order Created becomes Received
work order Approved becomes InProgress
estimate Approved remains Approved
WorkOrderIntakeRequests exists with unique RequestId
case-insensitive reference indexes reject Toyota/toyota duplicates
```

- [ ] **Step 4: Run every functional checkpoint test**

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
```

Expected: build and all three scopes PASS with Docker running.

- [ ] **Step 5: Verify contracts and commit the migration**

```powershell
rg -n 'POST /work-orders|work-orders/intake|work-orders/\{id.*\}/status' Adapters.Api Tests
if (rg -n "WorkOrderStatus\.(Created|Approved)|StartWork" Domain Application Adapters.Api Tests) { exit 1 }
git diff --check
git add Adapters.Infrastructure/DataAccess/Migrations Tests/E2E/WorkOrders/Phase2StatusMigrationE2eTests.cs
git commit -m "feat(database): migrate phase 2 work-order data"
```

Expected: both opening contracts and status route are present; no obsolete work-order status or StartWork symbol remains.

---

## Checkpoint Acceptance

The functional subproject is complete only when:

- both opening endpoints pass staff authorization and HTTP-contract tests;
- complete intake creates one atomic graph, reserves stock, and returns all IDs;
- sequential and concurrent idempotent retries obey `201/200/409` exactly;
- the status lifecycle, timestamps, domain events, cancellation, and stock releases match the matrix;
- the staff queue filters/orders in PostgreSQL while the customer history remains complete;
- the focused status query is side-effect free;
- the migration proves existing status data is preserved semantically;
- the worktree contains no deleted StartWork references and all focused/full tests above pass.
