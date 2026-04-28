# AGENTS.md - GarageFlow

## Mission
Guide Codex agents to evolve GarageFlow with maximum code quality, strict architecture boundaries, and consistent vertical-slice patterns across all modules.

## Core Engineering Principles
- Domain-Driven Design with explicit invariants in value objects.
- Vertical slice by module and use case.
- Clean dependency flow with no boundary violations.
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
- `GarageFlow.BuildingBlocks`
- `GarageFlow.Domain`
- `GarageFlow.Application`
- `GarageFlow.Infrastructure`
- `GarageFlow.Api`
- `GarageFlow.Tests.Shared`
- `GarageFlow.Tests.Unit`
- `GarageFlow.Tests.Integration`

## Dependency Direction (Non-negotiable)
- `Api -> Application -> Domain -> BuildingBlocks`
- `Infrastructure -> Application, Domain, BuildingBlocks`
- `Tests.Unit -> Application, Domain, Tests.Shared`
- `Tests.Integration -> Api, Infrastructure, Tests.Shared`
- API runtime types (`*Endpoint`, `*Endpoints`) must not depend directly on `Domain`, `Infrastructure`, or `BuildingBlocks`.
- `Domain` must never depend on `Api` or `Infrastructure`.
- `BuildingBlocks` must stay generic and independent of business modules.

## Canonical Module Layout
Every business module (Customers, Services, Vehicles, InventoryItems, Users, future modules) must follow the same shape:

- `Api/<Module>/<UseCase>/...Endpoint.cs`, `...Request.cs`, `...Response.cs`
- `Api/<Module>/<Module>Endpoints.cs` (module route registration + policy)
- `Application/<Module>/<UseCase>/...Command.cs|...Query.cs`, `...Handler.cs`, `...Result.cs|...Dto.cs`
- `Domain/<Module>/Entities/*.cs`
- `Domain/<Module>/ValueObjects/*.cs`
- `Domain/<Module>/Events/*.cs`
- `Domain/<Module>/Repositories/I...Repository.cs`
- `Domain/<Module>/Enums/*.cs` (if needed)
- `Infrastructure/<Module>/Configurations/*EntityConfiguration.cs`
- `Infrastructure/<Module>/Repositories/*Repository.cs`
- `Tests/Shared/<Module>/*Builder.cs`
- `Tests/Unit/<Module>/*Tests.cs`
- `Tests/Integration/Api/<Module>/*ApiTests.cs`

## Layer Responsibilities and Standards

### BuildingBlocks
- Keep only cross-module abstractions and primitives:
  - base entities and domain events
  - generic domain exceptions
  - generic reusable value objects
  - persistence contracts (`IUnitOfWork`)
- Do not add module-specific business rules here unless there is proven reuse.

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
- Repository contracts belong only in `Domain/<Module>/Repositories`.
- Domain cannot reference infrastructure concerns (DbContext, EF types, HTTP, etc.).

### Application
- One use-case per folder with explicit contract types.
- Commands/queries are immutable records.
- Handlers orchestrate use-cases, not domain rules.
- Input normalization/VO creation should happen before calling domain behavior.
- Mutating handlers must use `IUnitOfWork` transaction flow:
  - `BeginTransactionAsync`
  - `CommitTransactionAsync`
  - rollback in `catch`
- Query handlers must be side-effect free and not open transactions.
- Throw typed domain/application exceptions (`ValidationException`, `NotFoundException`, `BusinessRuleViolationException`) instead of ad hoc return codes.

### Api
- Minimal API endpoints must stay thin:
  - map request -> command/query
  - invoke mediator
  - map result -> response
- Endpoint implementation and module registration types must not depend on `Domain`, `Infrastructure`, or `BuildingBlocks` directly.
- No business rule implementation in endpoints.
- Request/response contracts should avoid domain coupling; any temporary exception must be explicitly tracked in drift-audit documentation.
- Each endpoint must define metadata:
  - `.WithName(...)`
  - `.WithTags(...)`
  - `.WithSummary(...)`
  - `.Produces...`/`.ProducesProblem...` for expected statuses
- Module endpoints must be registered through `<Module>Endpoints` and protected with the proper `SecurityPolicies`.

### Infrastructure
- Infrastructure implements domain contracts and persistence concerns only.
- EF Core configuration classes must map value objects using `HasConversion`.
- Keep constraints aligned with domain invariants (length, precision, required fields, indexes).
- DbContext is the single unit of work implementation and applies all module configurations.
- Repositories should not implement business decisions.

### Tests
- `Tests.Unit` focuses on Domain + Application behavior.
- `Tests.Integration` validates API behavior using InMemory database and mocked/controlled external integrations.
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
- JWT and authorization policies are centralized under `Api/Security`.
- Module route groups must apply required policies at group level.
- Exception-to-HTTP mapping is centralized in `UseGarageFlowExceptionHandler`.
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
dotnet test GarageFlow.slnx
```

Use focused filters during development, but final validation must include full-solution tests.

## Pull Request Completion Checklist
- Architecture boundaries respected.
- New/changed module follows canonical folder and naming pattern.
- Domain invariants live in value objects/entities (not leaked to API/Infrastructure).
- EF mapping mirrors domain model with correct conversions and constraints.
- Unit + integration tests added/updated and passing.
- No dead code, placeholder TODOs, or partial patterns left behind.
