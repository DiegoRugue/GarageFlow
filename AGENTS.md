# AGENTS.md - GarageFlow

## Mission
Guide Codex agents to evolve GarageFlow with maximum code quality, strict Clean Architecture boundaries, DDD tactical patterns, and consistent vertical-slice patterns across all modules.

## Repository Content Policy

- Keep application code, tests, infrastructure, automation, and required project configuration in the repository.
- Keep only Tech Challenge delivery artifacts, READMEs, ADRs, and RFCs as project documentation. Preserve the published deliverables from earlier phases.
- Store study notes, requirements tracking, implementation plans/specs, internal audits, agent briefs/reviews, and intermediate test or execution evidence outside the repository and every worktree, not merely in ignored folders.
- This policy also applies when a skill suggests creating planning or evidence files inside the checkout. Use the external study workspace instead.
- READMEs, ADRs, and RFCs must be self-contained and must not link to private work records or contain session transcripts, agent progress, or credential inventories.

## Core Engineering Principles
- Domain-Driven Design with explicit invariants in value objects.
- Vertical slice by module and use case.
- Clean Architecture with explicit inner/outer rings and no boundary violations.
- Adapters isolate framework, HTTP, persistence, and integration concerns from the application core.
- The `Host` project is the only composition root and executable entry point.
- Consistency over personal preference: follow existing GarageFlow patterns.
- Quality-first mindset: when touching out-of-pattern code, refactor it to the standard in the same scope whenever feasible.

## Official Technology Stack
- Language/runtime: C# on .NET 10 (`net10.0`) with `Nullable` enabled.
- API: ASP.NET Core Minimal APIs.
- Application messaging: `Mediator` (`IRequest` / `IRequestHandler`).
- Persistence: Entity Framework Core 10 with PostgreSQL (Npgsql) and InMemory provider for integration tests.
- Auth: JWT Bearer.
- Testing: xUnit + Moq + coverlet collector.
- Containers/dev infra: Docker + docker-compose (Postgres + pgAdmin + API).

## Solution Architecture
- `GarageFlow.SharedKernel`
- `GarageFlow.Domain`
- `GarageFlow.Application`
- `GarageFlow.Adapters.Api`
- `GarageFlow.Adapters.Infrastructure`
- `GarageFlow.Host`
- `GarageFlow.Tests.Shared`
- `GarageFlow.Tests.Unit`
- `GarageFlow.Tests.Integration`
- `GarageFlow.Tests.E2E`

## Architectural Rings
- `SharedKernel`: generic primitives shared by the inner rings.
- `Domain`: enterprise business rules, aggregates, value objects, enums, and domain events.
- `Application`: application business rules, use cases, ports, read models, orchestration, pipeline behaviors, and domain-event handlers.
- `Adapters.Api`: inbound HTTP adapter implemented with Minimal APIs.
- `Adapters.Infrastructure`: outbound adapter for EF Core, PostgreSQL, auth services, repositories, query implementations, migrations, and external integrations.
- `Host`: composition root, runtime configuration, middleware pipeline, OpenAPI/Scalar, authentication setup, adapter wiring, migrations/seed bootstrapping, and endpoint registration.
- `Tests`: verification projects organized by test scope.

## Dependency Direction (Non-negotiable)
- `Host -> Adapters.Api, Adapters.Infrastructure, Application, SharedKernel`
- `Adapters.Api -> Application`
- `Adapters.Infrastructure -> Application, Domain, SharedKernel`
- `Application -> Domain, SharedKernel`
- `Domain -> SharedKernel`
- `SharedKernel -> no GarageFlow production project`
- `Tests.Unit -> Application, Domain, Tests.Shared`
- `Tests.Integration -> Host, Adapters.Api, Adapters.Infrastructure, Tests.Shared`
- `Tests.E2E -> Host, Tests.Shared`
- API runtime types (`*Endpoint`, `*Endpoints`) must not depend directly on `Domain`, `Adapters.Infrastructure`, or `SharedKernel`.
- `Domain` must never depend on `Application`, `Adapters.Api`, `Adapters.Infrastructure`, or `Host`.
- `SharedKernel` must stay generic and independent of business modules.
- `Adapters.Api` and `Adapters.Infrastructure` must not depend on each other.
- `Host` may depend on `SharedKernel` only for cross-cutting runtime concerns such as centralized exception-to-HTTP mapping.

## Canonical Module Layout
Every business module (Customers, Services, Vehicles, InventoryItems, Users, future modules) must follow the same shape:

- `Adapters.Api/<Module>/<UseCase>/...Endpoint.cs`, `...Request.cs`, `...Response.cs`
- `Adapters.Api/<Module>/<Module>Endpoints.cs` (module route registration + policy)
- `Application/<Module>/UseCases/<UseCase>/...Command.cs|...Query.cs`, `...Handler.cs`, `...Result.cs|...Dto.cs`
- `Application/<Module>/Ports/I...Repository.cs`, `I...Queries.cs`
- `Application/<Module>/ReadModels/*ReadModel.cs|*Projection.cs|*Dto.cs`
- `Domain/<Module>/Entities/*.cs`
- `Domain/<Module>/ValueObjects/*.cs`
- `Domain/<Module>/Events/*.cs`
- `Domain/<Module>/Enums/*.cs` (if needed)
- `Adapters.Infrastructure/<Module>/Configurations/*EntityConfiguration.cs`
- `Adapters.Infrastructure/<Module>/Repositories/*Repository.cs|*Queries.cs`
- `Tests/Shared/<Module>/*Builder.cs`
- `Tests/Unit/<Module>/*Tests.cs`
- `Tests/Integration/Api/<Module>/*ApiTests.cs`
- `Tests/E2E/<Module>/*E2eTests.cs` when the module has end-to-end HTTP/database journeys.

## Layer Responsibilities and Standards

### SharedKernel
- Keep only cross-module abstractions and primitives:
  - base entities and domain events
  - generic domain exceptions
  - generic reusable value objects
  - persistence contracts (`IUnitOfWork`)
- Do not add module-specific business rules here unless there is proven reuse.
- Do not reference frameworks, adapters, persistence implementations, or application use cases.

### Domain
- Domain model is the source of truth for invariants.
- Entities/aggregates must:
  - be `sealed` where practical
  - expose behavior through methods (not public mutable state)
  - set `UpdatedAt` on mutations
  - raise domain events on create/update/delete and key state transitions
- Prefer value objects over primitive obsession:
  - validation and normalization belong in VOs
  - entities should not keep duplicated `Ensure*`/`Normalize*` logic when VO exists
- Repository and query contracts belong only in `Application/<Module>/Ports`.
- Domain cannot reference infrastructure concerns (DbContext, EF types, HTTP, etc.).
- Domain cannot use Mediator, application DTOs/read models, repository interfaces, or adapter contracts.

### Application
- One use-case per folder with explicit contract types.
- Commands/queries are immutable records.
- Handlers orchestrate use-cases, not domain rules.
- Input normalization/VO creation should happen before calling domain behavior.
- Mutating commands should implement the local command marker pattern used by `Application/Common/Messaging`.
- Mutating use cases are wrapped by `TransactionBehavior`; handlers must not manually begin, commit, or rollback transactions.
- The transaction pipeline is responsible for:
  - beginning the unit-of-work transaction
  - invoking the handler
  - saving changes
  - dequeuing domain events before commit
  - committing the transaction
  - dispatching domain events after commit
  - rolling back only when handler/save/commit fails before the commit completes
- Query handlers must be side-effect free and not open transactions.
- Throw typed domain/application exceptions (`ValidationException`, `NotFoundException`, `BusinessRuleViolationException`) instead of ad hoc return codes.
- Ports for repositories, query services, external services, hashing, tokens, email, and clocks belong in `Application`.
- Application may depend on `Domain` and `SharedKernel`, but never on API, infrastructure, host, EF Core, ASP.NET Core, or concrete external SDKs.
- Domain-event handlers that coordinate application side effects belong in `Application/<Module>/Events`.

### Adapters.Api
- Minimal API endpoints must stay thin:
  - map request -> command/query
  - invoke mediator
  - map result -> response
- Endpoint implementation and module registration types must not depend on `Domain`, `Adapters.Infrastructure`, or `SharedKernel` directly.
- No business rule implementation in endpoints.
- Request/response contracts must avoid domain coupling and should use API-owned primitive/string contracts for enum-like values.
- Each endpoint must define metadata:
  - `.WithName(...)`
  - `.WithTags(...)`
  - `.WithSummary(...)`
  - `.Produces...`/`.ProducesProblem...` for expected statuses
- Module endpoints must be registered through `<Module>Endpoints` and protected with the proper `SecurityPolicies`.
- Adapter API code may reference ASP.NET Core and security primitives, but must translate those concerns into application commands/queries.

### Adapters.Infrastructure
- Infrastructure implements application ports and persistence concerns only.
- EF Core configuration classes must map value objects using `HasConversion`.
- Keep constraints aligned with domain invariants (length, precision, required fields, indexes).
- DbContext is the single unit of work implementation and applies all module configurations.
- Repositories should not implement business decisions.
- Query implementations may project into application read models, but must not leak EF Core types outside the adapter.
- External integrations must implement `Application` abstractions and keep provider-specific details in this adapter.

### Host
- The `Host` project is the only executable project.
- `Program.cs` wires mediator, pipeline behaviors, domain-event dispatcher, authentication, infrastructure, OpenAPI/Scalar, middleware, and endpoint modules.
- The host may call adapter registration methods, but adapters must remain usable without referencing the host.
- Runtime-only concerns such as exception-to-HTTP mapping, migrations, seed execution, and environment-specific routes belong here.
- Do not place business rules, endpoint implementations, repositories, EF configurations, or domain behavior in `Host`.

### Tests
- `Tests.Unit` focuses on Domain + Application behavior.
- `Tests.Integration` validates API behavior using InMemory database and mocked/controlled external integrations.
- `Tests.E2E` validates real Host/API/PostgreSQL journeys with Testcontainers and requires Docker.
- `Tests.Shared` stores reusable test builders and request contracts.
- Required test coverage for each module:
  - domain invariants and domain events
  - handler success and failure paths
  - authorization behavior
  - HTTP status and payload shape

## Naming and Coding Conventions
- Use English for code symbols, class names, namespaces, and API contracts.
- Keep one public type per file.
- Suffix conventions:
  - `*Endpoint`, `*Command`, `*Query`, `*Handler`, `*Result`, `*Dto`, `*Repository`, `*EntityConfiguration`.
- Namespace mirrors folder path.
- Avoid magic literals; centralize constants in the owning type.
- Keep constructors defensive (`ArgumentNullException.ThrowIfNull` where needed).

## Security and Error Handling Standards
- JWT and authorization policies are centralized under `Adapters.Api/Security` and composed by `Host`.
- Module route groups must apply required policies at group level.
- Exception-to-HTTP mapping is centralized in `Host/Middlewares/UseGarageFlowExceptionHandler`.
- Do not leak internal exception detail for unexpected (`500`) errors.

## Quality Enforcement Rules
Whenever a touched area is out of pattern, enforce the standard before finishing:
- move duplicated validation to value objects
- align folder/namespace/use-case naming
- align endpoint metadata and policy usage
- align EF configuration and conversions with domain model
- align tests with the canonical module pattern

## Mandatory Verification Before Completion
Run and pass all relevant checks before claiming completion:

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
dotnet test GarageFlow.slnx
```

Use focused filters during development, but final validation must include full-solution tests. E2E/full-solution tests require Docker because Testcontainers starts PostgreSQL.

## Pull Request Completion Checklist
- Architecture boundaries respected.
- New/changed module follows canonical folder and naming pattern.
- Domain invariants live in value objects/entities (not leaked to `Adapters.Api` or `Adapters.Infrastructure`).
- EF mapping mirrors domain model with correct conversions and constraints.
- Unit, integration, and E2E tests added/updated where relevant and passing.
- No dead code, placeholder TODOs, or partial patterns left behind.
