# Work Orders Design

## Context

GarageFlow needs a core business module for service orders, called `WorkOrders` in code. A work order starts when a customer leaves a vehicle at the shop for diagnosis and services such as inspection, oil change, or corrective maintenance.

The system already has Customers, Vehicles, InventoryItems, Services, and Users. The new module must follow the existing GarageFlow architecture:

- vertical slice by module and use case;
- Domain-Driven Design with invariants in entities and value objects;
- API endpoints as thin Minimal API adapters;
- Application handlers as orchestration only;
- Infrastructure as persistence and repository implementation;
- transactions through `IUnitOfWork` for mutating use cases.

## Decisions

- A work order is implemented as a `WorkOrder` aggregate root.
- Estimates are child entities inside `WorkOrder`.
- Inventory items and services are estimate lines, not direct work-order lines.
- Customers become system users only through an explicit portal activation use case.
- `User` gains a `CustomerId?` link, and the new `Customer` role requires exactly one linked customer.
- Inventory stock is decreased when an inventory item is added to a draft estimate.
- Rejected estimates restore the stock reserved by their inventory lines.
- Estimates are built as `DRAFT` and submitted to `PENDING` when ready for customer decision.
- Estimate lines store snapshots of both customer-facing price and internal cost.
- Customer-facing API responses never expose inventory cost, total cost, margin, or equivalent internal cost fields.
- `WorkOrders` domain code may reference IDs and value objects from other Domain modules when that is part of the business language.
- Generic value objects from `BuildingBlocks` should be reused where their invariant matches the new use case.

## Domain Model

### WorkOrder

`WorkOrder` is the aggregate root and owns the lifecycle of the service order.

Properties:

- `WorkOrderId Id`
- `CustomerId CustomerId`
- `VehicleId VehicleId`
- `WorkOrderStatus Status`
- read-only estimate collection
- `CreatedAt`
- `UpdatedAt`

The aggregate validates:

- a work order starts as `Created`;
- customer and vehicle identifiers are not empty;
- status transitions are forward-only and explicit;
- finalized work orders cannot be changed;
- a work order cannot be completed without exactly one approved estimate;
- a work order cannot have multiple approved estimates.

### Estimate

`Estimate` is an entity inside `WorkOrder`.

Properties:

- `EstimateId Id`
- `EstimateStatus Status`
- inventory lines
- service lines
- calculated `TotalAmount`
- `CreatedAt`
- `UpdatedAt`

The entity validates:

- estimates start as `Draft`;
- only draft estimates can receive or remove lines;
- only draft estimates can be submitted;
- empty estimates cannot be submitted;
- only pending estimates can be approved or rejected;
- approved and rejected estimates cannot be changed.

### EstimateInventoryLine

Inventory estimate lines represent reserved parts or products.

Properties:

- `EstimateInventoryLineId Id`
- `InventoryItemId InventoryItemId`
- `Description DescriptionSnapshot`
- `EstimateItemQuantity Quantity`
- `Price UnitCost`
- `Price UnitPrice`
- calculated total price

`UnitCost` is persisted for internal management only. It must not be returned by customer-facing use cases.

`EstimateItemQuantity` is a new value object because estimate quantities must be greater than zero. Existing `InventoryItemStockQuantity` allows zero and should remain specific to stock quantity.

### EstimateServiceLine

Service estimate lines represent labor or service catalog entries.

Properties:

- `EstimateServiceLineId Id`
- `ServiceId ServiceId`
- `Description DescriptionSnapshot`
- `Price UnitPrice`
- calculated total price

### Reused Value Objects

Use existing `BuildingBlocks` value objects when their invariant is correct:

- `Price` for money values;
- `Description` for estimate line snapshots;
- `FullName`, `Email`, and existing user value objects in customer portal activation.

Create module-specific value objects for new invariants:

- `WorkOrderId`
- `EstimateId`
- `EstimateInventoryLineId`
- `EstimateServiceLineId`
- `EstimateItemQuantity`

## State Machines

### WorkOrderStatus

Statuses:

- `Created`
- `Diagnosing`
- `WaitingApproval`
- `Approved`
- `InProgress`
- `Completed`
- `Delivered`
- `Cancelled`

Allowed transitions:

```text
Created -> Diagnosing
Diagnosing -> WaitingApproval
WaitingApproval -> Approved
Approved -> InProgress
InProgress -> Completed
Completed -> Delivered

Created -> Cancelled
Diagnosing -> Cancelled
WaitingApproval -> Cancelled
Approved -> Cancelled
```

Rules:

- no status regression is allowed;
- `Completed`, `Delivered`, and `Cancelled` are terminal for content changes;
- `Delivered` only follows `Completed`;
- `Completed` requires exactly one approved estimate;
- changing estimates or lines is blocked once the work order is completed, delivered, or cancelled.
- customer approval of a pending estimate moves the work order from `WaitingApproval` to `Approved`;
- customer rejection of a pending estimate keeps the work order in `WaitingApproval` so staff can create and submit a replacement estimate.

### EstimateStatus

Statuses:

- `Draft`
- `Pending`
- `Approved`
- `Rejected`
- `Cancelled`

Allowed transitions:

```text
Draft -> Pending
Draft -> Cancelled
Pending -> Approved
Pending -> Rejected
```

Rules:

- an estimate starts as `Draft`;
- only `Draft` estimates can be edited;
- only non-empty `Draft` estimates can be submitted to `Pending`;
- only `Pending` estimates can be approved or rejected;
- an approved estimate cannot be rejected;
- a rejected estimate cannot be approved;
- approved, rejected, and cancelled estimates cannot be edited;
- rejecting an estimate restores reserved inventory stock;
- after rejection, a new estimate must be created if the shop needs another customer decision.

## Customer Portal and Authentication

Add `Customer` to `UserRole`.

Add nullable `CustomerId` to `User`.

Rules:

- `Admin` and `Attendant` users must not have `CustomerId`.
- `Customer` users must have `CustomerId`.
- the linked customer must exist.
- only one user can be linked to a customer in the initial design.
- portal activation must fail if another user already uses the customer's email.
- portal activation must fail if the customer already has a linked user.
- customer users follow the existing active-user rule: they cannot access active protected endpoints while `MustChangePassword` is true.

New use case:

```text
POST /customers/{customerId}/portal-user
```

Request:

```json
{
  "birthDate": "1990-01-01"
}
```

Behavior:

- load the customer;
- validate that no user is already linked to the customer;
- validate that the customer's email is not already used by another user;
- generate the initial password using the existing user pattern with customer full name and request birth date;
- create a `User` with role `Customer`, linked `CustomerId`, and `MustChangePassword = true`.

## Security Policies

Add:

- `SecurityRoles.Customer`
- `SecurityPolicies.ActiveCustomer`
- `SecurityPolicies.ActiveStaff`

Policy behavior:

- `ActiveCustomer`: authenticated role `Customer` and claim `must_change_password=false`;
- `ActiveStaff`: authenticated role `Admin` or `Attendant` and claim `must_change_password=false`;
- existing `ActiveAdmin` and `ActiveAttendant` remain available.

Internal work-order endpoints use `ActiveStaff`.

Customer portal work-order endpoints use `ActiveCustomer`.

## Application Use Cases

### Staff Use Cases

Create:

- `CreateWorkOrder`
- `CreateEstimate`
- `AddEstimateInventoryItem`
- `AddEstimateService`
- `SubmitEstimate`
- `StartDiagnosis`
- `StartWork`
- `CompleteWorkOrder`
- `DeliverWorkOrder`
- `CancelWorkOrder`
- `GetWorkOrderById`
- `ListWorkOrders`

Mutating handlers must use the standard transaction flow:

```text
BeginTransactionAsync
try
  mutate aggregate and related entities
  CommitTransactionAsync
catch
  RollbackTransactionAsync
```

Important handler rules:

- `CreateWorkOrder` validates that the customer exists.
- `CreateWorkOrder` validates that the vehicle exists.
- `CreateWorkOrder` validates that the vehicle belongs to the customer.
- `AddEstimateInventoryItem` validates the work order is editable.
- `AddEstimateInventoryItem` validates the estimate is draft.
- `AddEstimateInventoryItem` validates the inventory item exists.
- `AddEstimateInventoryItem` validates quantity is greater than zero.
- `AddEstimateInventoryItem` validates stock is sufficient.
- `AddEstimateInventoryItem` decreases stock and adds the estimate line in one transaction.
- `AddEstimateService` validates the service exists.
- `SubmitEstimate` prevents empty estimates and moves the work order to `WaitingApproval`.
- `ApproveMyEstimate` approves the estimate and moves the work order to `Approved`.
- `RejectMyEstimate` rejects the estimate, restores reserved stock, and keeps the work order in `WaitingApproval`.
- `CompleteWorkOrder` validates that one estimate is approved.

### Customer Use Cases

Create:

- `ListMyWorkOrders`
- `GetMyWorkOrderById`
- `ApproveMyEstimate`
- `RejectMyEstimate`

Rules:

- the authenticated user id is passed from API to Application;
- handlers load the user and validate the user is a customer user with `CustomerId`;
- customer queries are filtered by the linked `CustomerId`;
- customer commands validate that the work order belongs to the linked `CustomerId`;
- unauthorized cross-customer access returns not found semantics to avoid leaking existence;
- customer DTOs never include internal cost fields.

## API Endpoints

### Customer Portal Activation

```text
POST /customers/{customerId}/portal-user
```

Protected by `ActiveStaff`.

### Staff Work Order Endpoints

```text
POST   /work-orders
GET    /work-orders
GET    /work-orders/{id}
POST   /work-orders/{id}/estimates
POST   /work-orders/{id}/estimates/{estimateId}/inventory-items
POST   /work-orders/{id}/estimates/{estimateId}/services
POST   /work-orders/{id}/estimates/{estimateId}/submit
POST   /work-orders/{id}/start-diagnosis
POST   /work-orders/{id}/start-work
POST   /work-orders/{id}/complete
POST   /work-orders/{id}/deliver
POST   /work-orders/{id}/cancel
```

All staff endpoints are protected by `ActiveStaff`.

Each endpoint must define:

- `.WithName(...)`
- `.WithTags(...)`
- `.WithSummary(...)`
- `.Produces...`
- `.ProducesProblem...`

### Customer Work Order Endpoints

```text
GET  /me/work-orders
GET  /me/work-orders/{id}
POST /me/work-orders/{id}/estimates/{estimateId}/approve
POST /me/work-orders/{id}/estimates/{estimateId}/reject
```

All customer endpoints are protected by `ActiveCustomer`.

Approval and rejection by staff are intentionally out of scope for the initial module. If needed later, staff override must be a separate explicit and auditable use case.

## Persistence

Create infrastructure under `Infrastructure/WorkOrders`.

Tables:

```text
WorkOrders
WorkOrderEstimates
WorkOrderEstimateInventoryLines
WorkOrderEstimateServiceLines
```

Recommended columns:

```text
WorkOrders
- Id
- CustomerId
- VehicleId
- Status
- CreatedAt
- UpdatedAt

WorkOrderEstimates
- Id
- WorkOrderId
- Status
- CreatedAt
- UpdatedAt

WorkOrderEstimateInventoryLines
- Id
- EstimateId
- InventoryItemId
- Description
- Quantity
- UnitCost
- UnitPrice

WorkOrderEstimateServiceLines
- Id
- EstimateId
- ServiceId
- Description
- UnitPrice
```

EF Core mapping:

- map all IDs through `HasConversion`;
- map status enums with `HasConversion<string>()`;
- map `Price` and `Description` through conversions;
- configure required fields and max lengths aligned with value object invariants;
- map relationships from `WorkOrder` to estimates and from estimates to lines;
- ignore domain events;
- add `DbSet<WorkOrder>` to `GarageFlowDbContext`;
- apply `WorkOrderEntityConfiguration`;
- register `IWorkOrderRepository`.

Indexes:

- `WorkOrders(CustomerId)`
- `WorkOrders(VehicleId)`
- `WorkOrderEstimates(WorkOrderId, Status)`
- unique filtered PostgreSQL index for at most one approved estimate per work order, while keeping the domain rule as the primary protection.

User persistence changes:

- add nullable `CustomerId` to `Users`;
- index `Users(CustomerId)`;
- enforce unique linked customer for customer users where supported by provider;
- keep domain and handler validation as the primary cross-provider guarantee.

## Repositories and Read Models

Create:

```text
Domain/WorkOrders/Repositories/IWorkOrderRepository.cs
Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs
```

Repository capabilities:

- get aggregate by id with estimates and lines;
- list internal work-order details;
- get internal work-order details by id;
- list customer-facing work-order details by customer;
- get customer-facing work-order details by id and customer;
- add a new work order.

Use separate read models for internal and customer-facing responses to reduce cost-leak risk.

Internal DTOs may include:

- inventory `UnitCost`;
- inventory `UnitPrice`;
- inventory `TotalPrice`;
- service `UnitPrice`;
- service `TotalPrice`;
- estimate total.

Customer DTOs may include:

- description;
- quantity;
- unit price;
- line total;
- estimate total;
- work order and estimate status.

Customer DTOs must not include:

- `UnitCost`;
- `TotalCost`;
- margin;
- inventory item internal cost;
- any field named or shaped like internal cost.

## Domain Events

Add focused domain events for important business transitions:

- `WorkOrderCreated`
- `WorkOrderStatusChanged`
- `EstimateCreated`
- `EstimateSubmitted`
- `EstimateApproved`
- `EstimateRejected`
- `EstimateInventoryLineAdded`
- `EstimateServiceLineAdded`

Events should follow existing project style and remain domain notifications only. No asynchronous event dispatch is required for this module unless the broader project adds that infrastructure later.

## Testing Strategy

### Unit Tests: Domain

Create `Tests/Unit/WorkOrders/WorkOrderTests.cs`.

Required scenarios:

- create work order with status `Created`;
- prevent status regression;
- prevent changes after `Completed`, `Delivered`, or `Cancelled`;
- prevent completion without an approved estimate;
- complete with an approved estimate;
- prevent multiple approved estimates;
- create draft estimate;
- prevent submitting empty estimate;
- submit estimate as pending;
- approve pending estimate;
- reject pending estimate;
- prevent approving rejected estimate;
- prevent rejecting approved estimate;
- prevent changing approved estimate;
- prevent changing rejected estimate;
- calculate estimate total as inventory lines plus service lines.

### Unit Tests: Handlers

Create `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`.

Required scenarios:

- create work order validates customer;
- create work order validates vehicle;
- prevent creation when vehicle does not belong to customer;
- add inventory item with sufficient stock;
- prevent adding inventory item with insufficient stock;
- decrease stock when adding inventory item;
- restore stock when rejecting estimate;
- prevent invalid quantity;
- add valid service;
- prevent missing service;
- submit estimate as pending;
- complete work order with approved estimate;
- commit successful mutations;
- rollback mutations that fail after transaction begins.

Update `Tests/Unit/Users/UserHandlersTests.cs`.

Required scenarios:

- activate customer portal user;
- prevent portal activation for missing customer;
- prevent portal activation when customer already has linked user;
- prevent portal activation when customer email is already used by another user;
- enforce `Customer` role and linked `CustomerId` invariants.

### Integration Tests

Create `Tests/Integration/Api/WorkOrders/WorkOrdersApiTests.cs`.

Required scenarios:

- create work order returns `201` and status `Created`;
- prevent creation with vehicle from another customer;
- add inventory item and verify stock decreases;
- prevent insufficient stock;
- prevent submitting empty estimate;
- customer lists only own work orders;
- customer accessing another customer's work order returns `404`;
- customer approves own pending estimate;
- customer approving another customer's estimate returns `404`;
- customer rejects own pending estimate and stock is restored;
- customer responses do not include internal cost fields.

### Architecture Tests

Update architecture tests to confirm:

- API work-order endpoint types do not reference Domain, Infrastructure, or BuildingBlocks directly;
- Domain still does not reference API or Infrastructure;
- BuildingBlocks remains generic and independent;
- Application orchestrates use cases without owning state-machine rules.

## Verification

Focused development can use filtered unit and integration tests, but final validation must run:

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test GarageFlow.slnx
```

## Out of Scope

- staff approval or rejection override;
- billing, invoices, payments, and receipts;
- scheduling;
- technician assignment;
- file attachments, photos, or signatures;
- multiple customer users for the same customer;
- stock reservation expiration;
- automatic customer portal activation on customer creation.

## Acceptance Criteria

- Work orders follow the defined state machine.
- Estimates follow the defined state machine.
- A work order cannot be completed without one approved estimate.
- A work order cannot have more than one approved estimate.
- Inventory stock is decreased when items are added to estimates.
- Inventory stock is restored when pending estimates are rejected.
- Estimate totals are calculated from snapshot line values.
- Customer portal users can access only their own work orders and estimates.
- Customer-facing responses never expose inventory cost.
- The module follows the canonical GarageFlow vertical-slice layout.
- Full build and test commands pass before implementation is claimed complete.
