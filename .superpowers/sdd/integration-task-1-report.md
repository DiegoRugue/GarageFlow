# Integration Reliability Task 1 Report

## Outcome

Implemented application-level orchestration for external estimate decisions, limited to the Application and Unit test layers.

Feature commit: `419fb44` (`feat(work-orders): orchestrate external estimate decisions`)

Review correction commit: `7279405` (`test(work-orders): prove transaction rollback composition`)

## Files changed

- `Application/WorkOrders/Common/EstimateDecisionInboxRegistration.cs`
- `Application/WorkOrders/Ports/IEstimateDecisionInbox.cs`
- `Application/WorkOrders/UseCases/ReceiveEstimateDecision/ReceiveEstimateDecisionCommand.cs`
- `Application/WorkOrders/UseCases/ReceiveEstimateDecision/ReceiveEstimateDecisionHandler.cs`
- `Application/WorkOrders/UseCases/ReceiveEstimateDecision/ReceiveEstimateDecisionResult.cs`
- `Tests/Unit/WorkOrders/ReceiveEstimateDecisionHandlerTests.cs`

No production file changed in the review correction.

## TDD evidence

### Red

Command:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~ReceiveEstimateDecisionHandlerTests"
```

Result: failed as expected in 7.2 seconds with exit code 1. Compilation reported `CS0234` and `CS0246` because the `ReceiveEstimateDecision` slice and `IEstimateDecisionInbox` did not exist. This demonstrated that the tests depended on the new contracts and behavior rather than passing against existing production code.

### Green

Command:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~ReceiveEstimateDecisionHandlerTests|FullyQualifiedName~EstimateDecisionProcessorTests"
```

Result: passed 23/23 tests, 0 failed, 0 skipped, in 12.4 seconds total command time (318 ms reported test duration). The run emitted the pre-existing analyzer warning `CA1822` for `EstimateDecisionProcessor.Approve`; no new warning originated from the Task 1 files.

### Review correction red/green

The original rollback-named test invoked only `ReceiveEstimateDecisionHandler.Handle`, so it could not observe the transaction boundary or prove rollback. It was replaced with a composition test around the existing `TransactionBehavior<ReceiveEstimateDecisionCommand, ReceiveEstimateDecisionResult>`, using the real handler and real `EstimateDecisionProcessor` as the `next` path.

RED command:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~TransactionBehavior_WhenProcessingFailsAfterRegistration_RollsBackWithoutCommitOrDispatch" --no-restore
```

Result: failed with exit code 1 because the strict composition fixture did not yet expose its real handler or support disabling broad default mock setups (`CS1739`); the missing Mediator delegate import also produced `CS0246`.

GREEN command:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~ReceiveEstimateDecisionHandlerTests|FullyQualifiedName~EstimateDecisionProcessorTests|FullyQualifiedName~TransactionBehaviorTests" --no-restore
```

Result: passed 27/27 tests, 0 failed, 0 skipped. The strict `MockSequence` proves `BeginTransactionAsync -> inbox registration -> work-order load -> rejection processor inventory lookup/failure -> RollbackTransactionAsync`. The test also proves no commit, no domain-event dequeue, and no domain-event dispatch.

## Full verification

Command:

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
```

Result after the review correction: passed 500/500 tests, 0 failed, 0 skipped, in 10.4 seconds total command time.

Command:

```powershell
dotnet build GarageFlow.slnx
```

Result after the review correction: build succeeded in 4.0 seconds total command time (3.01 seconds reported by MSBuild), with 0 warnings and 0 errors.

Command:

```powershell
git diff --cached --check
```

Result: passed before the feature commit; only Git's informational LF-to-CRLF working-copy notices were printed during staging.

## Behavior covered

- New approval and rejection, including rejection stock release.
- Case-insensitive input with canonical decision dispatch.
- Same-hash duplicate short-circuit before aggregate load/processing.
- Different-hash duplicate conflict.
- Empty event, work-order, and estimate identifiers.
- Invalid decision values.
- Uppercase, non-hexadecimal, short, and long payload hashes.
- Non-UTC occurred-at timestamps.
- Missing work order with the established not-found wording.
- Received-at timestamp supplied by `TimeProvider`.
- Cancellation-token propagation through inbox, work-order repository, and rejection stock lookup.
- Transaction composition with the real command handler: begin precedes inbox registration; registration precedes aggregate loading and processor failure; the existing `TransactionBehavior` invokes rollback and skips commit, event dequeue, and event dispatch.
- `ICorrelatedCommand` correlation using the event ID in `D` format.

## Assumptions

- The existing `EstimateDecisionProcessor` remains the shared domain-orchestration component and is exercised as real code; its collaborators are mocked only at the application port boundaries.
- Transaction begin/commit/rollback remains owned by the existing `TransactionBehavior`. The focused composition test proves pipeline rollback invocation after a downstream failure; it does not claim that an EF-persisted inbox row was reverted.
- Validation intentionally occurs before inbox registration in the deterministic order required by the task.

## Deferred work

No API endpoint, webhook HMAC verification, EF Core inbox persistence, migration, outbox, SNS integration, or Host wiring was implemented. Persistence-level proof that rollback removes an EF inbox row remains explicitly deferred to Task 2 with the Infrastructure inbox implementation.

Integration, E2E, and full-solution test runs were not required by the delegated Task 1 completion command set. The full solution was compiled, and the complete Unit project was executed.
