# Clean Architecture + DDD Modular Migration Design

## Context

GarageFlow is currently organized around `Api`, `Application`, `Domain`, `Infrastructure`, and `BuildingBlocks`. The architecture is already inspired by Clean Architecture and DDD, but the project intentionally allows refactoring because it is a study project and will not go to production.

The new target is a more explicit Clean Architecture model while preserving GarageFlow's modular DDD shape. The migration should make the conceptual boundaries obvious in project names, namespaces, folders, dependency rules, and tests.

## Goals

- Rename layers so the solution communicates Clean Architecture concepts clearly.
- Keep business modules visible and consistent across layers.
- Make `Domain` a pure business core with entities, value objects, domain events, enums, and invariants only.
- Move ports, repository contracts, query contracts, read models, and application DTOs to `Application`.
- Treat API and infrastructure as adapters around the application core.
- Introduce a dedicated executable host as the composition root.
- Migrate incrementally so each implementation phase is testable and reviewable.

## Non-Goals

- Do not change business behavior during naming-only phases.
- Do not redesign HTTP routes unless a route currently violates architecture boundaries.
- Do not replace EF Core, Mediator, Minimal APIs, JWT, xUnit, or PostgreSQL.
- Do not merge all modules into one vertical-slice project; modules remain visible inside each architectural circle.

## Target Projects

The target solution should contain these production projects:

```text
GarageFlow.SharedKernel
GarageFlow.Domain
GarageFlow.Application
GarageFlow.Adapters.Api
GarageFlow.Adapters.Infrastructure
GarageFlow.Host
```

The target test projects should be:

```text
GarageFlow.Tests.Shared
GarageFlow.Tests.Unit
GarageFlow.Tests.Integration
GarageFlow.Tests.E2E
```

`GarageFlow.Host` is the only executable application project. It owns `Program.cs`, application startup, DI composition, configuration, OpenAPI registration, auth registration, migrations, seeding, exception-to-HTTP mapping, middleware pipeline, and endpoint registration.

`GarageFlow.Adapters.Api` is an inbound adapter. It owns Minimal API endpoint definitions, HTTP request/response contracts, HTTP mappers, API security helpers, OpenAPI metadata helpers, and HTTP problem-details response helpers that do not require direct domain exception references.

`GarageFlow.Adapters.Infrastructure` is an outbound adapter. It owns EF Core, PostgreSQL, migrations, entity configurations, repository implementations, query implementations, JWT generation, password hashing, email implementations, outbox persistence, and other external integrations.

## Dependency Rules

Allowed dependencies:

```text
GarageFlow.Host -> GarageFlow.Adapters.Api
GarageFlow.Host -> GarageFlow.Adapters.Infrastructure
GarageFlow.Host -> GarageFlow.Application

GarageFlow.Adapters.Api -> GarageFlow.Application

GarageFlow.Adapters.Infrastructure -> GarageFlow.Application
GarageFlow.Adapters.Infrastructure -> GarageFlow.Domain
GarageFlow.Adapters.Infrastructure -> GarageFlow.SharedKernel

GarageFlow.Application -> GarageFlow.Domain
GarageFlow.Application -> GarageFlow.SharedKernel

GarageFlow.Domain -> GarageFlow.SharedKernel

GarageFlow.SharedKernel -> no GarageFlow production project
```

Forbidden dependencies:

- `Domain` must not depend on `Application`, `Adapters.Api`, `Adapters.Infrastructure`, or `Host`.
- `Application` must not depend on `Adapters.Api`, `Adapters.Infrastructure`, or `Host`.
- `Adapters.Api` must not depend on `Domain`, `Adapters.Infrastructure`, `Host`, or `SharedKernel` directly.
- `Adapters.Infrastructure` must not depend on `Adapters.Api` or `Host`.
- `SharedKernel` must not contain GarageFlow business-module rules.

The only project that may know all production layers is `GarageFlow.Host`.

## SharedKernel Structure

`GarageFlow.SharedKernel` replaces the current `GarageFlow.BuildingBlocks` project.

Target structure:

```text
GarageFlow.SharedKernel/
  Domain/
    Entities/
    Events/
    Exceptions/
    ValueObjects/
  Application/
    Pagination/
    Results/
  Persistence/
    IUnitOfWork.cs
```

Allowed content:

- Generic base entity and domain event primitives.
- Generic domain exceptions used across modules.
- Generic reusable value objects with proven cross-module meaning.
- Generic pagination/result primitives.
- Generic persistence abstractions such as `IUnitOfWork`.

Forbidden content:

- Customer, vehicle, service, inventory, user, or work-order-specific rules.
- EF Core, ASP.NET Core, JWT, Mediator handlers, repository implementations, or API contracts.

## Domain Structure

`GarageFlow.Domain` is the pure business model.

Target structure:

```text
GarageFlow.Domain/
  Customers/
    Entities/
    ValueObjects/
    Events/
    Enums/
  WorkOrders/
    Entities/
    ValueObjects/
    Events/
    Enums/
  InventoryItems/
    Entities/
    ValueObjects/
    Events/
    Enums/
  Services/
    Entities/
    ValueObjects/
    Events/
    Enums/
  Users/
    Entities/
    ValueObjects/
    Events/
    Enums/
  Vehicles/
    Entities/
    ValueObjects/
    Events/
    Enums/
```

Domain may contain:

- Aggregate roots and child entities.
- Value objects and strongly typed identifiers.
- Domain events.
- Domain enums.
- Domain invariants and state transitions.
- Pure domain services only when an invariant does not naturally belong to one aggregate.

Domain must not contain:

- Repository interfaces.
- Query interfaces.
- Read models.
- DTOs or result contracts.
- Mediator request/handler types.
- EF Core or database details.
- HTTP, JWT, logging, email, file, or external integration details.

## Application Structure

`GarageFlow.Application` owns use cases and ports.

Target structure:

```text
GarageFlow.Application/
  Customers/
    UseCases/
      CreateCustomer/
      UpdateCustomer/
      GetCustomerById/
      ListCustomers/
    Ports/
      ICustomerRepository.cs
      ICustomerQueries.cs
    ReadModels/
  WorkOrders/
    UseCases/
    Ports/
    ReadModels/
  InventoryItems/
    UseCases/
    Ports/
    ReadModels/
  Services/
    UseCases/
    Ports/
    ReadModels/
  Users/
    UseCases/
    Ports/
    ReadModels/
  Vehicles/
    UseCases/
    Ports/
    ReadModels/
  Common/
    Behaviors/
    Exceptions/
    Mapping/
    Pagination/
    Time/
```

Application may contain:

- Commands, queries, handlers, results, and application DTOs.
- Ports for persistence, queries, time, identity generation, email, auth token generation, hashing, and outbox publishing.
- Read models and projections returned by query ports.
- Pipeline behaviors for validation, transactions, logging, and domain event dispatch.
- Application-level exceptions and orchestration rules.

Application must not contain:

- ASP.NET Core endpoint definitions or HTTP contracts.
- EF Core, SQL, Npgsql, or migration details.
- JWT implementation details.
- Business invariants that belong in domain entities or value objects.

## Api Adapter Structure

`GarageFlow.Adapters.Api` owns inbound HTTP adaptation.

Target structure:

```text
GarageFlow.Adapters.Api/
  Customers/
    CreateCustomer/
      CreateCustomerEndpoint.cs
      CreateCustomerHttpRequest.cs
      CreateCustomerHttpResponse.cs
      CreateCustomerHttpMapper.cs
    CustomerEndpoints.cs
  WorkOrders/
  InventoryItems/
  Services/
  Users/
  Vehicles/
  Common/
    ProblemDetails/
    OpenApi/
  Security/
```

The API adapter may contain:

- Minimal API endpoints.
- Endpoint route group registration.
- HTTP request and response contracts.
- HTTP-to-application mappers.
- API security helpers and policy names.
- ProblemDetails response helpers that do not depend directly on Domain or SharedKernel exception types.
- OpenAPI metadata helpers.

The API adapter must not contain:

- EF Core or database access.
- Domain entities, value objects, enums, or exceptions in public HTTP contracts.
- Infrastructure implementations.
- Business rules beyond HTTP binding and authorization extraction.

Recommended contract suffixes:

```text
*HttpRequest
*HttpResponse
*Endpoint
*Endpoints
*HttpMapper
```

If verbosity becomes excessive, `*Request` and `*Response` may remain only inside `Adapters.Api`, but they must be documented as HTTP-only contracts.

## Infrastructure Adapter Structure

`GarageFlow.Adapters.Infrastructure` owns outbound implementation details.

Target structure:

```text
GarageFlow.Adapters.Infrastructure/
  Persistence/
    GarageFlowDbContext.cs
    Configurations/
    Migrations/
    UnitOfWork/
  Customers/
    EfCustomerRepository.cs
    EfCustomerQueries.cs
  WorkOrders/
    EfWorkOrderRepository.cs
    EfWorkOrderQueries.cs
  InventoryItems/
  Services/
  Users/
  Vehicles/
  Auth/
  Email/
  Outbox/
  Time/
```

Infrastructure may contain:

- EF Core DbContext and entity configurations.
- PostgreSQL/Npgsql-specific persistence.
- Repository implementations for aggregate mutation.
- Query/read-service implementations for projections.
- JWT token service implementation.
- Password hash service implementation.
- Email service implementation.
- Clock and ID generator implementations.
- Outbox storage and dispatcher implementations.

Infrastructure must not contain:

- HTTP endpoint definitions.
- API request/response contracts.
- Host startup logic.
- Business invariants that belong in Domain.

Recommended implementation suffixes:

```text
Ef*Repository
Ef*Queries
*EntityConfiguration
*TokenService
*PasswordHashService
*EmailSender
```

## Host Structure

`GarageFlow.Host` is the composition root.

Target structure:

```text
GarageFlow.Host/
  Program.cs
  Configuration/
  DependencyInjection/
    AddApplication.cs
    AddApiAdapter.cs
    AddInfrastructureAdapter.cs
  appsettings.json
  appsettings.Development.json
  appsettings.Production.json
```

Host responsibilities:

- Build the web application.
- Register `Application`, `Adapters.Api`, and `Adapters.Infrastructure`.
- Configure authentication and authorization.
- Configure OpenAPI and Scalar.
- Configure exception handling middleware and exception-to-HTTP mapping.
- Configure database provider, migrations, and seed execution.
- Map endpoint groups exposed by `Adapters.Api`.

Host must not contain business rules.

## Naming Migration Map

Project and folder renames:

```text
BuildingBlocks                    -> SharedKernel
Api                               -> Adapters.Api
Infrastructure                    -> Adapters.Infrastructure
Api/Program.cs                    -> Host/Program.cs
Application/<Module>/<UseCase>    -> Application/<Module>/UseCases/<UseCase>
Domain/<Module>/Repositories      -> Application/<Module>/Ports and Application/<Module>/ReadModels
```

Type renames:

```text
API *Request / *Response          -> *HttpRequest / *HttpResponse
Domain *ReadModel                 -> Application *ReadModel or *Projection
Domain I*Repository               -> Application/Ports I*Repository
Read-oriented repository methods  -> Application/Ports I*Queries
Infrastructure *Repository        -> Ef*Repository
Infrastructure read service       -> Ef*Queries
```

`Dto` naming should be reduced over time. Prefer:

- `*Result` for use case output.
- `*ReadModel` for application query projections.
- `*HttpResponse` for API output.
- `*HttpRequest` for API input.

## Example WorkOrders Target Shape

Before:

```text
Domain/WorkOrders/Repositories/IWorkOrderRepository.cs
Domain/WorkOrders/Repositories/WorkOrderDetailsReadModel.cs
Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs
Application/WorkOrders/GetWorkOrderById/GetWorkOrderByIdHandler.cs
Api/WorkOrders/GetWorkOrderById/GetWorkOrderByIdEndpoint.cs
```

After:

```text
Application/WorkOrders/Ports/IWorkOrderRepository.cs
Application/WorkOrders/Ports/IWorkOrderQueries.cs
Application/WorkOrders/ReadModels/WorkOrderDetailsReadModel.cs
Adapters.Infrastructure/WorkOrders/EfWorkOrderRepository.cs
Adapters.Infrastructure/WorkOrders/EfWorkOrderQueries.cs
Application/WorkOrders/UseCases/GetWorkOrderById/GetWorkOrderByIdHandler.cs
Adapters.Api/WorkOrders/GetWorkOrderById/GetWorkOrderByIdEndpoint.cs
```

Mutation flow:

```text
HTTP -> Adapters.Api endpoint -> Application command handler -> Application port -> Infrastructure EF repository -> Domain aggregate
```

Query flow:

```text
HTTP -> Adapters.Api endpoint -> Application query handler -> Application query port -> Infrastructure EF queries -> Application read model -> API HTTP response
```

## Migration Phases

### Phase 1: Project and Namespace Rename Without Behavior Change

Rename projects and namespaces while preserving current behavior:

- `BuildingBlocks` to `SharedKernel`.
- `Api` to `Adapters.Api`.
- `Infrastructure` to `Adapters.Infrastructure`.
- Keep the current executable temporarily if needed, but prepare for `Host`.

Acceptance criteria:

- Solution builds.
- Existing unit and integration tests pass.
- Architecture tests are updated for new project names.
- No business behavior is intentionally changed.

### Phase 2: Introduce Host Composition Root

Create `GarageFlow.Host` and move executable startup concerns there.

Acceptance criteria:

- `Host` owns `Program.cs`.
- `Adapters.Api` exposes endpoint registration extensions but is not executable.
- `Adapters.Infrastructure` exposes service registration extensions but does not depend on `WebApplication`.
- E2E and integration test factories target `GarageFlow.Host`.

### Phase 3: Move Ports and Read Models to Application

Move repository contracts, query contracts, and read models out of `Domain`.

Acceptance criteria:

- `Domain/<Module>/Repositories` no longer exists.
- Application owns `Ports` and `ReadModels`.
- Infrastructure implements Application ports.
- Domain contains no read models, repository interfaces, or query contracts.

### Phase 4: Split Mutation Repositories From Query Services

Separate aggregate mutation ports from read/query ports.

Acceptance criteria:

- `I*Repository` ports load and persist aggregates for commands.
- `I*Queries` ports return application read models for queries.
- EF implementations use `Ef*Repository` and `Ef*Queries` naming.
- Query handlers do not load full aggregates when a projection is enough.

### Phase 5: Harden API Adapter Contracts

Remove direct Domain and SharedKernel usage from API contracts and endpoint runtime types.

Acceptance criteria:

- API request/response contracts use primitive, string, or API-owned enum contract types.
- API endpoints depend on Application only.
- Host-level HTTP error handling maps application and SharedKernel exceptions without leaking infrastructure details.
- Protected endpoints document 401 and 403 consistently.

### Phase 6: Add Cross-Cutting Application Behaviors

Move repeated handler infrastructure concerns into Application pipeline behaviors where supported by Mediator.

Acceptance criteria:

- Command transaction flow is centralized.
- Domain event dispatch happens after successful persistence.
- Domain events are cleared after dispatch.
- External effects are handled through event handlers or outbox instead of ad hoc post-commit code in use cases.

### Phase 7: Strengthen Architecture Tests and Documentation

Update architecture and module convention tests to protect the new model.

Acceptance criteria:

- Tests enforce all target dependency rules.
- Tests scan all API adapter runtime and contract types, not only `*Endpoint`.
- Tests prevent `Domain` from containing `Repositories`, `ReadModels`, `Dto`, `Mediator`, EF, or HTTP references.
- AGENTS.md and README architecture sections describe the new layer names.

## Testing Strategy

Unit tests:

- Domain invariants and domain events remain in `GarageFlow.Tests.Unit`.
- Application handler tests target use cases and mock Application ports.
- Architecture tests enforce dependency and folder rules.

Integration tests:

- API integration tests run through `GarageFlow.Host`.
- Infrastructure repository/query tests target `Adapters.Infrastructure` implementations.
- InMemory provider remains acceptable for fast API contract tests.

E2E tests:

- `GarageFlow.Tests.E2E` remains the PostgreSQL/Testcontainers suite.
- E2E project should be included in the solution so full validation can include real database flows.

Final migration validation:

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
dotnet test GarageFlow.slnx
```

## Implementation Plan Requirements

The implementation plan should be phase-based. Each phase should:

- Have a narrow objective.
- Start with architecture or compilation tests when feasible.
- Avoid mixing mechanical renames with behavior changes.
- Update project references and namespaces consistently.
- Run focused validation before moving to the next phase.
- Preserve current public API behavior unless a change is explicitly documented.

The plan should avoid a single large migration commit. Each phase should be independently reviewable.

## Risks and Mitigations

Risk: large namespace rename creates noisy diffs.  
Mitigation: isolate mechanical rename phases and avoid behavior edits in the same phase.

Risk: moving `Program.cs` breaks integration test factories.  
Mitigation: update test factories in the same Host phase and validate API smoke tests immediately.

Risk: moving repository contracts changes many handlers at once.  
Mitigation: move ports module by module, starting with one representative module such as Services or InventoryItems before WorkOrders.

Risk: splitting repositories and queries increases type count.  
Mitigation: apply split where query projections exist; keep simple repositories simple until there is a real read model.

Risk: API contract renaming becomes excessive.  
Mitigation: prefer `*HttpRequest` and `*HttpResponse` for new or touched endpoints, and migrate existing contracts in batches.

## Success Criteria

The migration is complete when:

- The solution uses the target project names.
- `GarageFlow.Host` is the only executable composition root.
- `GarageFlow.Domain` contains no ports, repositories, read models, DTOs, Mediator, EF, HTTP, JWT, or infrastructure concerns.
- `GarageFlow.Application` owns use cases, ports, read models, and cross-cutting application behaviors.
- `GarageFlow.Adapters.Api` maps HTTP to Application without referencing Domain or Infrastructure.
- `GarageFlow.Adapters.Infrastructure` implements Application ports and owns persistence/external integrations.
- Architecture tests enforce the new dependency model.
- Unit, integration, E2E, and full-solution tests pass.
