# Functional Task 2 Report — Work-order lifecycle and StartWork removal

## Status

Implemented and verified. The work-order lifecycle now uses `Received`, `Diagnosing`, `WaitingApproval`, `InProgress`, `Completed`, `Delivered`, and `Cancelled`, preserving numeric value `4` as unused. `EstimateStatus.Approved` remains unchanged.

## Implementation

- Replaced `WorkOrderStatus.Created` with `Received = 1` and removed only `WorkOrderStatus.Approved`; `InProgress` remains `5`.
- Made estimate approval transition `WaitingApproval -> InProgress`, set `StartedAt` to the approval timestamp, and emit the exact status-change event.
- Made estimate rejection transition `WaitingApproval -> Diagnosing` after rejecting the estimate.
- Restricted `StartDiagnosis()` to `Received -> Diagnosing`, so the backward transition from waiting approval occurs only through rejection.
- Kept first estimate submission as two explicit transitions: `Received -> Diagnosing -> WaitingApproval`.
- Required `InProgress` before starting an approved estimate service; service start no longer starts the work order.
- Allowed cancellation from `InProgress` and removed all work-order `Approved` branches.
- Deleted the StartWork command, handler, and endpoint, and removed route registration.
- Updated Unit, Integration, and E2E lifecycle/status expectations. The removed `/start-work` route is verified as `404 Not Found`.
- Did not implement stock restoration; that remains Task 3.

## Files

- Modified: `Domain/WorkOrders/Enums/WorkOrderStatus.cs`
- Modified: `Domain/WorkOrders/Entities/WorkOrder.cs`
- Deleted: `Application/WorkOrders/UseCases/StartWork/StartWorkCommand.cs`
- Deleted: `Application/WorkOrders/UseCases/StartWork/StartWorkHandler.cs`
- Deleted: `Adapters.Api/WorkOrders/StartWork/StartWorkEndpoint.cs`
- Modified: `Adapters.Api/WorkOrders/WorkOrderEndpoints.cs`
- Added: `Tests/Unit/WorkOrders/WorkOrderLifecycleTests.cs`
- Modified: `Tests/Unit/WorkOrders/WorkOrderTests.cs`
- Modified: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`
- Modified: `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`
- Modified: `Tests/E2E/WorkOrders/WorkOrdersE2eTests.cs`

## TDD evidence

### RED

Command:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrderTests|FullyQualifiedName~WorkOrderLifecycleTests" --no-restore
```

After correcting two overly strict timestamp assertions in the new tests, the intentional RED was:

- Failed: 4; Passed: 71; Total: 75.
- New work order expected `Received`, actual `Created`.
- Approval expected `InProgress`, actual `Approved`.
- Rejection expected `Diagnosing`, actual `WaitingApproval`.
- Cancellation fixture expected approval to have entered `InProgress`, actual `Approved`.

### GREEN

The same domain filter after implementation passed 71/71. The complete WorkOrders Unit filter passed 115/115, and the WorkOrders API Integration filter passed 27/27.

## Final verification

Commands executed:

```powershell
dotnet build GarageFlow.slnx --no-restore
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --no-restore
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --no-restore
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --no-restore
dotnet test GarageFlow.slnx --no-restore
```

Results:

- Build: succeeded, 0 warnings, 0 errors.
- Unit: 447 passed, 0 failed.
- Integration: 107 passed, 0 failed.
- E2E: 19 passed, 0 failed.
- Full solution: Unit 447, Integration 107, E2E 19; 0 failures.

## Self-review

- Clean Architecture boundaries remain unchanged: lifecycle rules stay in Domain; API only lost the redundant route; Application only lost the redundant command slice.
- Enum values match the brief exactly and value `4` is unused.
- Every allowed lifecycle transition is covered with exact previous/new status events and timestamp assertions; skipped/backward public operations are rejected.
- Approval uses one timestamp for `StartedAt`, `UpdatedAt`, the status event, and `EstimateApproved.ApprovedAt`.
- Rejection changes lifecycle only after `Estimate.Reject()` succeeds.
- `EstimateStatus.Approved` and approved-estimate execution rules were preserved.
- No stock restoration behavior was added.

## Concerns

None blocking. Stock-reservation restoration on rejection is intentionally deferred to Functional Task 3.
