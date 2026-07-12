# GarageFlow Phase 2 Integration Reliability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a replay-resistant signed estimate-decision webhook and durable work-order status notifications through a transactional outbox, a safe multi-pod publisher, and Amazon SNS email.

**Architecture:** The inbound HTTP adapter verifies the timestamped HMAC over the untouched request bytes, then sends a correlated Application command. Application atomically registers the external event, reuses the shared estimate-decision processor, and maps committed domain events to stable integration messages; Infrastructure owns inbox/outbox persistence, leasing, retry, and SNS delivery.

**Tech Stack:** C#/.NET 10, ASP.NET Core Minimal APIs, `System.Security.Cryptography`, Mediator, EF Core 10/Npgsql, PostgreSQL 17, `BackgroundService`, AWS SDK for .NET SNS 4.0.100.3, xUnit, Moq, Testcontainers.

## Global Constraints

- Begin only after the functional-compliance checkpoint passes and migration `20260711090000_WorkOrderFunctionalCompliance` is present.
- Retain authenticated customer approve/reject endpoints; add anonymous `POST /webhooks/estimate-decisions` protected solely by HMAC and inbox replay prevention.
- Require headers `X-GarageFlow-Timestamp` and `X-GarageFlow-Signature`; sign exact bytes as `<timestamp>.<raw-body>` using HMAC-SHA256.
- Accept at most five minutes of absolute clock skew; require a 64-character lowercase hexadecimal signature and constant-time comparison.
- Read at most 65,536 body bytes, validate the signature before JSON deserialization, and never log raw bodies, signatures, secrets, or AWS credentials.
- Treat `eventId` as the inbox primary key: equal ID/hash returns `204` without a second transition; equal ID/different hash returns `409`.
- Register the inbox row, mutate the work order, restore stock when rejected, and commit in one transaction.
- Treat PostgreSQL E2E as the acceptance proof for inbox/outbox atomicity and concurrency; EF InMemory tests cover only sequential behavior because that provider cannot roll back a successful first save when a later pipeline stage fails.
- Preserve the transaction contract: first save domain state, stage/dequeue domain events, persist outbox, second save, commit, then dispatch the original events through Mediator.
- Map stable event key `work-order.status-changed.v1`; never store assembly-qualified CLR names.
- Make status notification delivery exclusively outbox-owned; delete the logging approval-email handler, abstraction, adapter, registration, and tests.
- Publisher claims must use PostgreSQL row locks with `SKIP LOCKED`, finite leases, conditional ownership updates, bounded exponential retry, and bounded error text.
- Delivery is at-least-once: a crash after SNS publish but before marking processed may produce a duplicate email.
- SNS content includes work-order ID, previous status, current status, and occurrence time.
- Keep the worker disabled in ordinary local/integration settings and enabled explicitly in Kubernetes or publisher-lifecycle tests.
- Use `us-east-1`; the confirmed email subscription is a deployment prerequisite, not an application responsibility.

---

## File Structure

### Create — webhook and inbox

- `Application/WorkOrders/Common/EstimateDecisionInboxRegistration.cs`
- `Application/WorkOrders/Ports/IEstimateDecisionInbox.cs`
- `Application/WorkOrders/UseCases/ReceiveEstimateDecision/ReceiveEstimateDecisionCommand.cs`
- `Application/WorkOrders/UseCases/ReceiveEstimateDecision/ReceiveEstimateDecisionResult.cs`
- `Application/WorkOrders/UseCases/ReceiveEstimateDecision/ReceiveEstimateDecisionHandler.cs`
- `Adapters.Api/Webhooks/EstimateDecisions/EstimateDecisionWebhookRequest.cs`
- `Adapters.Api/Webhooks/EstimateDecisions/EstimateDecisionWebhookOptions.cs`
- `Adapters.Api/Webhooks/EstimateDecisions/EstimateDecisionWebhookSignatureValidator.cs`
- `Adapters.Api/Webhooks/EstimateDecisions/ReceiveEstimateDecisionWebhookEndpoint.cs`
- `Adapters.Api/Webhooks/EstimateDecisionWebhookExtensions.cs`
- `Adapters.Api/Webhooks/WebhookEndpoints.cs`
- `Adapters.Infrastructure/WorkOrders/Inbox/EstimateDecisionInboxEvent.cs`
- `Adapters.Infrastructure/WorkOrders/Configurations/EstimateDecisionInboxEventEntityConfiguration.cs`
- `Adapters.Infrastructure/WorkOrders/Repositories/EstimateDecisionInboxRepository.cs`
- `Adapters.Infrastructure/DataAccess/Migrations/20260711100000_EstimateDecisionInbox.cs` and `.Designer.cs`

### Create — outbox and SNS

- `Application/Common/Integrations/IntegrationOutboxMessage.cs`
- `Application/Common/Integrations/IIntegrationOutboxMapper.cs`
- `Application/Common/Integrations/IOutboxWriter.cs`
- `Application/WorkOrders/Common/Integrations/WorkOrderStatusChangedIntegrationEvent.cs`
- `Application/WorkOrders/Common/Integrations/WorkOrderIntegrationOutboxMapper.cs`
- `Application/WorkOrders/Ports/IWorkOrderStatusNotificationPublisher.cs`
- `Application/Common/Integrations/OutboxRetrySchedule.cs`
- `Adapters.Infrastructure/Integrations/Outbox/OutboxMessage.cs`
- `Adapters.Infrastructure/Integrations/Outbox/OutboxMessageEntityConfiguration.cs`
- `Adapters.Infrastructure/Integrations/Outbox/EfOutboxWriter.cs`
- `Adapters.Infrastructure/Integrations/Outbox/OutboxRepository.cs`
- `Adapters.Infrastructure/Integrations/Outbox/IntegrationOutboxProcessor.cs`
- `Adapters.Infrastructure/Integrations/Outbox/IntegrationOutboxPublisher.cs`
- `Adapters.Infrastructure/Integrations/Outbox/IntegrationOutboxPublisherOptions.cs`
- `Adapters.Infrastructure/WorkOrders/Notifications/AmazonSnsStatusNotificationOptions.cs`
- `Adapters.Infrastructure/WorkOrders/Notifications/AmazonSnsWorkOrderStatusNotificationPublisher.cs`
- `Adapters.Infrastructure/DataAccess/Migrations/20260711110000_IntegrationOutbox.cs` and `.Designer.cs`

### Create — tests

- `Tests/Unit/WorkOrders/ReceiveEstimateDecisionHandlerTests.cs`
- `Tests/Unit/WorkOrders/WorkOrderIntegrationOutboxMapperTests.cs`
- `Tests/Unit/Common/OutboxRetryScheduleTests.cs`
- `Tests/Integration/Api/Webhooks/EstimateDecisionWebhookSignatureValidatorTests.cs`
- `Tests/Integration/Api/Webhooks/EstimateDecisionWebhookApiTests.cs`
- `Tests/Integration/WorkOrders/EstimateDecisionInboxRepositoryTests.cs`
- `Tests/Integration/Common/GarageFlowDbContextDomainEventTests.cs`
- `Tests/Integration/Integrations/OutboxPersistenceTests.cs`
- `Tests/Integration/Integrations/OutboxRepositoryTests.cs`
- `Tests/Integration/Integrations/IntegrationOutboxProcessorTests.cs`
- `Tests/Integration/WorkOrders/AmazonSnsWorkOrderStatusNotificationPublisherTests.cs`
- `Tests/E2E/WorkOrders/EstimateDecisionWebhookE2eTests.cs`
- `Tests/E2E/Integrations/OutboxConcurrencyE2eTests.cs`
- `Tests/E2E/Support/Fakes/RecordingWorkOrderStatusNotificationPublisher.cs`

### Modify

- `Application/Common/Behaviors/TransactionBehavior.cs`
- `SharedKernel/Persistence/IUnitOfWork.cs` documentation only; signatures stay unchanged.
- `Adapters.Infrastructure/DataAccess/GarageFlowDbContext.cs`
- `Adapters.Infrastructure/DataAccess/DependencyInjection.cs`
- `Adapters.Infrastructure/DataAccess/Migrations/GarageFlowDbContextModelSnapshot.cs`
- `Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj`
- `Host/Program.cs`, all three Host appsettings files, and Integration/E2E factory settings.
- `Tests/Unit/Common/TransactionBehaviorTests.cs`
- `Tests/Integration/GarageFlow.Tests.Integration.csproj` — add Moq 4.20.72 for the controlled SNS client.

### Delete

- `Application/WorkOrders/Abstractions/ICustomerApprovalEmailSender.cs`
- `Application/WorkOrders/Events/SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler.cs`
- `Adapters.Infrastructure/WorkOrders/Email/LoggingCustomerApprovalEmailSender.cs`
- Their DI registration and their three handler tests in `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`.

---

### Task 1: Add application-level external decision orchestration

**Files:**
- Create: inbox port/registration and `ReceiveEstimateDecision` slice
- Test: `Tests/Unit/WorkOrders/ReceiveEstimateDecisionHandlerTests.cs`

**Interfaces:**
- Consumes: `ICorrelatedCommand`, `EstimateDecisionProcessor`, `IWorkOrderRepository.GetByIdForEstimateMutationAsync` from the functional plan.
- Produces: `IEstimateDecisionInbox.RegisterAsync(...)` and the command/result below.

- [ ] **Step 1: Write strict failing handler tests**

Test new approval, new rejection and stock release, same-hash duplicate, different-hash conflict, empty IDs, invalid decision, non-lowercase/incorrect-length hash, non-UTC `OccurredAt`, missing work order, and inbox rollback behavior. A duplicate must prove the work-order repository and processor are never called.

- [ ] **Step 2: Run the handler tests and confirm types are absent**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~ReceiveEstimateDecisionHandlerTests"
```

Expected: FAIL at compilation because the receive-decision slice does not exist.

- [ ] **Step 3: Define the command, result, and inbox port**

```csharp
public sealed record ReceiveEstimateDecisionCommand(
    Guid EventId,
    Guid WorkOrderId,
    Guid EstimateId,
    string Decision,
    DateTime OccurredAt,
    string PayloadHash)
    : ICommand<ReceiveEstimateDecisionResult>, ICorrelatedCommand
{
    public string CorrelationId => EventId.ToString("D");
}

public sealed record ReceiveEstimateDecisionResult(bool IsDuplicate);

public sealed record EstimateDecisionInboxRegistration(
    bool IsNew,
    string StoredPayloadHash);

public interface IEstimateDecisionInbox
{
    Task<EstimateDecisionInboxRegistration> RegisterAsync(
        Guid eventId,
        string payloadHash,
        DateTime occurredAt,
        DateTime receivedAt,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement validation and deterministic handler order**

The handler injects inbox, work-order repository, shared processor, and `TimeProvider`. It must:

1. reject empty IDs;
2. accept only `Approved` or `Rejected`, case-insensitively, then use the canonical names;
3. require `OccurredAt.Kind == DateTimeKind.Utc` and a 64-character lowercase SHA-256 hash;
4. call `RegisterAsync(..., timeProvider.GetUtcNow().UtcDateTime, ...)`;
5. return duplicate before loading the aggregate when hashes match;
6. throw `BusinessRuleViolationException("Event ID '{id}' was already used with a different payload.")` when hashes differ;
7. lock/load the aggregate and throw the existing work-order `NotFoundException` message;
8. call `EstimateDecisionProcessor.Approve` or `RejectAsync`;
9. return `IsDuplicate=false`.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~ReceiveEstimateDecisionHandlerTests|FullyQualifiedName~EstimateDecisionProcessorTests"
git add Application/WorkOrders Tests/Unit/WorkOrders
git commit -m "feat(work-orders): orchestrate external estimate decisions"
```

Expected: PASS.

---

### Task 2: Verify raw HMAC and persist the inbox atomically

**Files:**
- Create: all webhook API and inbox Infrastructure files listed above
- Create: inbox migration `20260711100000_EstimateDecisionInbox`
- Modify: `GarageFlowDbContext`, Infrastructure DI, Host registration/settings, test factories
- Test: webhook signature, API, and inbox repository tests

**Interfaces:**
- Consumes: `ReceiveEstimateDecisionCommand` from Task 1.
- Produces: anonymous `POST /webhooks/estimate-decisions` and atomic `IEstimateDecisionInbox` implementation.

- [ ] **Step 1: Write signature tests with a controlled clock**

Use a fixed `TimeProvider` and construct signatures from exact UTF-8 request bytes. Cover valid signature, one-byte body tamper, whitespace/property-order change, uppercase hex, malformed hex, missing headers, nonnumeric timestamp, exactly ±300 seconds accepted, and ±301 seconds rejected.

- [ ] **Step 2: Implement the bounded signature validator**

Options use section `Webhooks:EstimateDecisions`, property `HmacSecret`, and fixed `AllowedClockSkewSeconds = 300`. The public method is:

```csharp
public bool IsValid(
    string? timestampHeader,
    string? signatureHeader,
    ReadOnlyMemory<byte> rawBody);
```

Implementation rules:

```csharp
var prefix = Encoding.ASCII.GetBytes(timestampHeader + ".");
var signedBytes = new byte[prefix.Length + rawBody.Length];
prefix.CopyTo(signedBytes, 0);
rawBody.Span.CopyTo(signedBytes.AsSpan(prefix.Length));

using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.HmacSecret));
var expected = hmac.ComputeHash(signedBytes);
return CryptographicOperations.FixedTimeEquals(expected, supplied);
```

Reject before allocation when body length exceeds 65,536 bytes or signature format is not exactly lowercase hex.

- [ ] **Step 3: Add the inbox entity, mapping, and claim SQL**

Map table `EstimateDecisionInboxEvents`:

```text
EventId uuid primary key
PayloadHash varchar(64) not null
OccurredAt timestamp with time zone not null
ReceivedAt timestamp with time zone not null
```

For PostgreSQL execute inside the command transaction:

```sql
INSERT INTO "EstimateDecisionInboxEvents"
    ("EventId", "PayloadHash", "OccurredAt", "ReceivedAt")
VALUES (@eventId, @payloadHash, @occurredAt, @receivedAt)
ON CONFLICT ("EventId") DO NOTHING;
```

One affected row returns new. Zero affected rows loads the stored hash. The concurrent loser waits for the winner; winner failure rolls the insert back. InMemory may use tracked lookup/add only for sequential API tests.

- [ ] **Step 4: Generate and normalize the deterministic inbox migration**

```powershell
dotnet ef migrations add EstimateDecisionInbox --project Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj --startup-project Host/GarageFlow.Host.csproj
```

Rename the pair and `[Migration]` identity to `20260711100000_EstimateDecisionInbox`; verify it follows the functional migration and updates the snapshot once.

- [ ] **Step 5: Implement the anonymous endpoint without rebinding the body**

Read `HttpRequest.Body` into memory with a 65,536-byte hard limit. Extract the two headers, validate HMAC, then deserialize those same bytes with web JSON options into:

```csharp
public sealed record EstimateDecisionWebhookRequest(
    Guid EventId,
    Guid WorkOrderId,
    Guid EstimateId,
    string Decision,
    DateTime OccurredAt);
```

Compute lowercase SHA-256 from the untouched body bytes, send the command, and return `Results.NoContent()` for both new and duplicate results. Return typed `400` for signed malformed JSON and throw `UnauthorizedAccessException("Webhook signature is invalid or expired.")` for transport authentication failure.

Register `.AllowAnonymous()` through a separate `WebhookEndpoints` module in Host, never through the staff/customer WorkOrder groups. Add complete endpoint metadata for `204/400/401/404/409/500`.

- [ ] **Step 6: Validate settings without exposing the secret**

Register `TimeProvider.System`, bind options, and fail non-Development/non-Integration startup if the HMAC secret is empty, shorter than 32 characters, or contains a known configuration marker. Add a non-secret local development value through environment/Compose later; tests inject a deterministic 32+ character value.

- [ ] **Step 7: Run signature, repository, and API tests**

```powershell
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~EstimateDecisionWebhook|FullyQualifiedName~EstimateDecisionInbox"
```

Expected: PASS for valid approve/reject, missing/invalid/expired signature, duplicate, conflicting body, malformed signed JSON, missing aggregate, and transactional inbox rollback.

- [ ] **Step 8: Commit the signed ingress**

```powershell
git add Adapters.Api/Webhooks Application/WorkOrders Adapters.Infrastructure Host Tests/Integration
git commit -m "feat(api): add signed estimate decision webhook"
```

---

### Task 3: Persist integration messages inside TransactionBehavior

**Files:**
- Create: Application outbox contracts, WorkOrder integration event/mapper, Infrastructure entity/config/writer
- Modify: `TransactionBehavior`, `GarageFlowDbContext`, DI, snapshot
- Create: migration `20260711110000_IntegrationOutbox`
- Delete: the legacy logging email path
- Test: transaction, mapper, deleted-event, and persistence tests

**Interfaces:**
- Produces: stable `IntegrationOutboxMessage`, mapper, writer, and notification event.
- Changes: `CommitTransactionAsync` becomes commit-only; `SaveChangesAsync` stages domain events.

- [ ] **Step 1: Rewrite pipeline tests before production code**

Use `MockSequence` or an operation log to assert exact success order:

```text
Begin, Handler, Save1, Dequeue, Map, Write, Save2, Commit, Dispatch
```

Also test no-message path skips Write/Save2, and failures at Handler/Save1/Map/Write/Save2/Commit roll back without dispatch. A post-commit dispatch failure must not roll back. Add a correlated command assertion and an Activity trace fallback assertion.

- [ ] **Step 2: Define outbox contracts and stable WorkOrder event**

```csharp
public sealed record IntegrationOutboxMessage(
    Guid Id,
    string EventKey,
    Guid AggregateId,
    string Payload,
    DateTime OccurredAt,
    string? CorrelationId);

public interface IIntegrationOutboxMapper
{
    IReadOnlyList<IntegrationOutboxMessage> Map(
        IReadOnlyCollection<DomainEvent> domainEvents,
        string? correlationId);
}

public interface IOutboxWriter
{
    Task WriteAsync(
        IReadOnlyCollection<IntegrationOutboxMessage> messages,
        CancellationToken cancellationToken);
}

public sealed record WorkOrderStatusChangedIntegrationEvent(
    Guid WorkOrderId,
    string PreviousStatus,
    string CurrentStatus,
    DateTime OccurredAt)
{
    public const string EventKey = "work-order.status-changed.v1";
}
```

The mapper handles only `WorkOrderStatusChanged`, maps `UpdatedAt -> OccurredAt`, serializes stable camel-case JSON, uses `Guid.NewGuid()` for message ID, and ignores unrelated events.

- [ ] **Step 3: Stage events across the first EF save**

Add `_stagedDomainEvents` to `GarageFlowDbContext`. `SaveChangesAsync` snapshots events from tracked `IHasDomainEvents`, calls `base.SaveChangesAsync`, then—only on success—clears entity events and appends the snapshot. Preserve the functional checkpoint's PostgreSQL unique-violation-to-`BusinessRuleViolationException` translation around the base save. `DequeueDomainEvents` drains and clears the staged list. `RollbackTransactionAsync` clears both the ChangeTracker and staged list.

This ordering is mandatory because EF detaches deleted entities after save; reading only the ChangeTracker after save would lose their events.

- [ ] **Step 4: Make commit commit-only and implement the new pipeline**

`CommitTransactionAsync` returns immediately when no relational transaction exists; otherwise it commits/disposes without calling save. `TransactionBehavior` performs the exact sequence from Step 1 and resolves correlation as:

```csharp
var correlationId = message is ICorrelatedCommand correlated
    ? correlated.CorrelationId
    : Activity.Current?.TraceId.ToHexString();
```

- [ ] **Step 5: Map and persist the outbox entity**

Table `IntegrationOutboxMessages` has:

```text
Id uuid primary key
EventKey varchar(128) not null
AggregateId uuid not null
Payload jsonb not null
OccurredAt timestamptz not null
CorrelationId varchar(64) null
AttemptCount integer not null default 0
NextAttemptAt timestamptz not null
ProcessedAt timestamptz null
LastError varchar(1024) null
LeaseId uuid null
LeaseExpiresAt timestamptz null
```

`EfOutboxWriter.WriteAsync` maps all messages into tracked adapter entities with `NextAttemptAt = OccurredAt` and never saves.

- [ ] **Step 6: Generate deterministic outbox migration and partial index**

Generate `IntegrationOutbox`, rename its identity to `20260711110000_IntegrationOutbox`, and add a PostgreSQL partial index ordered by `NextAttemptAt`, `OccurredAt`, `Id` with filter `"ProcessedAt" IS NULL`.

- [ ] **Step 7: Remove the old email delivery path**

Delete the three files listed in File Structure, their registration, and their handler tests. Keep `EstimateWaitingApprovalRequested` only if other domain behavior/tests still require the event; it no longer sends email.

- [ ] **Step 8: Run unit and persistence tests**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~TransactionBehaviorTests|FullyQualifiedName~WorkOrderIntegrationOutboxMapperTests"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~GarageFlowDbContextDomainEventTests|FullyQualifiedName~OutboxPersistenceTests"
```

Expected: PASS, including rollback boundaries and deleted-entity event capture.

- [ ] **Step 9: Commit the durable write path**

```powershell
git add Application SharedKernel Adapters.Infrastructure Host Tests
git commit -m "feat(integrations): persist status notifications transactionally"
```

---

### Task 4: Claim, retry, and publish outbox messages safely

**Files:**
- Create: retry schedule and all Infrastructure outbox repository/processor/worker files
- Test: retry, repository, and processor test files

**Interfaces:**
- Produces: batch claim, conditional mark/reschedule, processor, and hosted poller.
- Consumes: `IWorkOrderStatusNotificationPublisher` implemented in Task 5.

- [ ] **Step 1: Write pure retry-schedule tests**

With the default options, the first through later failures yield `5s, 10s, 20s, 40s, 80s, 160s, 300s, 300s`. The pure function accepts the configured bounds:

```csharp
TimeSpan ForAttempt(int attempt, TimeSpan initialDelay, TimeSpan maxDelay);

Assert.Equal(
    TimeSpan.FromSeconds(5),
    OutboxRetrySchedule.ForAttempt(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(300)));
Assert.Equal(
    TimeSpan.FromSeconds(300),
    OutboxRetrySchedule.ForAttempt(20, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(300)));
```

Also prove a non-default initial delay and cap are honored, and reject attempt values below one or non-positive/inverted bounds.

- [ ] **Step 2: Write PostgreSQL leasing tests**

Prove due ordering, not-due exclusion, processed exclusion, active-lease exclusion, expired-lease reclaim, two workers obtaining disjoint IDs, attempt increment on claim, and mark/reschedule refusing a stale lease owner.

- [ ] **Step 3: Implement short-transaction claims**

`ClaimBatchAsync(Guid leaseId, DateTime now, DateTime leaseExpiresAt, int batchSize, ...)` starts its own Infrastructure transaction, selects due rows with:

```sql
SELECT *
FROM "IntegrationOutboxMessages"
WHERE "ProcessedAt" IS NULL
  AND "NextAttemptAt" <= @now
  AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" <= @now)
ORDER BY "NextAttemptAt", "OccurredAt", "Id"
FOR UPDATE SKIP LOCKED
LIMIT @batchSize;
```

While locked, assign the shared lease ID, set expiry, increment `AttemptCount`, save, commit, and return detached claimed rows. Publish outside this transaction.

- [ ] **Step 4: Implement lease-owned completion updates**

`MarkProcessedAsync` and `RescheduleAsync` use conditional `ExecuteUpdateAsync` restricted by message ID, lease ID, and `ProcessedAt == null`. Success sets `ProcessedAt` and clears lease/error; failure sets bounded `LastError`, next attempt, and clears the lease. Return `true` only when exactly one row changed.

- [ ] **Step 5: Implement processor dispatch and retry**

The processor switches on `EventKey`. For `work-order.status-changed.v1`, deserialize `WorkOrderStatusChangedIntegrationEvent`, call the notification publisher, then mark processed. Unsupported key or malformed JSON is rescheduled with a bounded diagnostic; do not drop it. On publisher error, pass `claimed.AttemptCount` plus the configured initial/maximum delays to `OutboxRetrySchedule.ForAttempt(...)`, and truncate the stored error to 1,024 characters.

- [ ] **Step 6: Implement the hosted poller**

Bind section `Integrations:Outbox` with exact defaults:

```text
Enabled=false
BatchSize=10
PollingIntervalSeconds=5
LeaseDurationSeconds=300
InitialRetryDelaySeconds=5
MaxRetryDelaySeconds=300
```

`IntegrationOutboxPublisher : BackgroundService` creates a fresh scope for each poll, invokes the processor, uses `PeriodicTimer` rather than `Task.Delay` loops, honors cancellation, and emits structured IDs/status counts without payloads or secrets.

- [ ] **Step 7: Run retry, repository, and processor tests**

```powershell
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~OutboxRetryScheduleTests"
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~OutboxRepositoryTests|FullyQualifiedName~IntegrationOutboxProcessorTests"
```

Expected: PASS, including disjoint multi-worker claims.

- [ ] **Step 8: Commit the worker**

```powershell
git add Application/Common/Integrations Adapters.Infrastructure/Integrations Tests
git commit -m "feat(integrations): process outbox with leases and retry"
```

---

### Task 5: Publish work-order status through Amazon SNS

**Files:**
- Create: SNS port/options/adapter and controlled-client tests
- Modify: Infrastructure project, DI, Host settings, test factories

**Interfaces:**
- Produces: `IWorkOrderStatusNotificationPublisher.PublishAsync(WorkOrderStatusChangedIntegrationEvent, CancellationToken)`.
- Consumes: AWS default credential chain supplied by Kubernetes environment variables and topic ARN configuration.

- [ ] **Step 1: Add the exact AWS SDK dependency and test dependency**

In Infrastructure:

```xml
<PackageReference Include="AWSSDK.SimpleNotificationService" Version="4.0.100.3" />
```

In Integration tests:

```xml
<PackageReference Include="Moq" Version="4.20.72" />
```

- [ ] **Step 2: Define the publisher port and options**

```csharp
public interface IWorkOrderStatusNotificationPublisher
{
    Task PublishAsync(
        WorkOrderStatusChangedIntegrationEvent notification,
        CancellationToken cancellationToken);
}
```

`AmazonSnsStatusNotificationOptions` binds `Integrations:Sns`, requires `Region = "us-east-1"` and a nonempty `TopicArn` when the outbox worker is enabled.

- [ ] **Step 3: Write a controlled-client SNS contract test**

Mock `IAmazonSimpleNotificationService.PublishAsync(PublishRequest, CancellationToken)`. Capture the request and assert topic ARN, a subject shorter than SNS's limit, and message text containing the exact work-order ID, previous/current names, and ISO-8601 UTC time. Assert cancellation token forwarding and AWS exception propagation.

- [ ] **Step 4: Implement the SNS adapter**

Construct `PublishRequest` explicitly:

```csharp
var request = new PublishRequest
{
    TopicArn = options.TopicArn,
    Subject = $"GarageFlow work order {notification.CurrentStatus}",
    Message = string.Join(Environment.NewLine,
        $"Work order: {notification.WorkOrderId:D}",
        $"Previous status: {notification.PreviousStatus}",
        $"Current status: {notification.CurrentStatus}",
        $"Occurred at: {notification.OccurredAt:O}")
};

await client.PublishAsync(request, cancellationToken);
```

- [ ] **Step 5: Wire conditional runtime services**

When `Integrations:Outbox:Enabled=true`, register an `AmazonSimpleNotificationServiceClient` for `us-east-1`, SNS adapter, processor, and hosted worker. Default credential resolution reads the temporary `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` injected by Kubernetes. When disabled, do not start the worker or require SNS settings.

- [ ] **Step 6: Run SNS and processor tests**

```powershell
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~AmazonSnsWorkOrderStatusNotificationPublisherTests|FullyQualifiedName~IntegrationOutboxProcessorTests"
```

Expected: PASS with no live AWS calls.

- [ ] **Step 7: Commit SNS delivery**

```powershell
git add Application/WorkOrders/Ports Adapters.Infrastructure Host Tests/Integration
git commit -m "feat(notifications): publish work-order status through sns"
```

---

### Task 6: Prove PostgreSQL concurrency and the full delivery lifecycle

**Files:**
- Create: webhook/outbox E2E tests and recording publisher
- Modify: E2E factory to enable worker only for the dedicated collection

**Interfaces:**
- Proves: inbox serialization, work-order row locking, outbox persistence, disjoint leases, retry, and eventual processing.

- [ ] **Step 1: Add real signed webhook journeys**

Use raw `ByteArrayContent` so the HMAC bytes are exact. Cover external approval into `InProgress`, rejection into `Diagnosing` with exact stock restoration, concurrent same-event duplicates, same ID/different body conflict, invalid/expired signatures, and failed transition rolling back the inbox row so a corrected retry can succeed.

- [ ] **Step 2: Add an in-process recording publisher**

Implement `IWorkOrderStatusNotificationPublisher` with a `Channel<WorkOrderStatusChangedIntegrationEvent>` and deterministic failure injection. Never replace the outbox repository; the E2E test must use real PostgreSQL rows and only replace the external SNS boundary.

- [ ] **Step 3: Add publisher lifecycle and lease tests**

Assert status transition creates a pending row; worker publishes and marks it processed; injected first failure increments attempt and reschedules; two processors claim disjoint messages; expired lease is reclaimed; processed rows are never claimed.

- [ ] **Step 4: Run the dedicated E2E suites**

```powershell
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~EstimateDecisionWebhookE2eTests|FullyQualifiedName~OutboxConcurrencyE2eTests"
```

Expected: PASS with Docker running and no AWS credentials.

- [ ] **Step 5: Run the complete integration checkpoint**

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
```

Expected: PASS; `rg -n "ICustomerApprovalEmailSender|LoggingCustomerApprovalEmailSender|SendApprovalEmailWhen" Application Adapters.Infrastructure Tests` returns no matches.

- [ ] **Step 6: Commit PostgreSQL reliability coverage**

```powershell
git add Tests/E2E Tests/Integration
git commit -m "test(integrations): verify inbox and outbox concurrency"
```

---

## Checkpoint Acceptance

The integration-reliability subproject is complete only when:

- signed raw-body validation passes all tamper, format, and skew boundaries;
- new, duplicate, conflicting, concurrent, and rolled-back inbox events behave exactly as specified;
- authenticated and webhook decisions share one approval/rejection implementation;
- each committed work-order status change writes a stable outbox record in the same transaction;
- the original domain events still dispatch after commit and deleted-entity events are not lost;
- multiple publishers claim disjoint rows and stale owners cannot mark/reschedule them;
- failures remain pending with bounded exponential retry and structured logs;
- the controlled SNS test verifies exact content without a live AWS call;
- the old logging email path is absent and all checkpoint tests pass.
