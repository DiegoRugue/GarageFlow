# Real API E2E Design

## Context

GarageFlow already has strong unit tests and integration tests, but the integration fixture runs the API with the EF Core InMemory provider. That is useful for fast API contract checks, but it does not exercise PostgreSQL/Npgsql query translation. Recent production-like failures were caused by LINQ expressions that passed with InMemory and failed with Npgsql.

The new E2E automation must run against the real API and a real PostgreSQL database. Its goal is not only to avoid `500 Internal Server Error`; it must validate the expected HTTP contract for each journey:

- expected status codes;
- expected response bodies;
- expected ProblemDetails shape for validation, authorization, not-found, and business-rule failures;
- expected state transitions and related resources observable through API reads.

## Decision

Create a separate E2E test project:

```text
Tests/E2E/GarageFlow.Tests.E2E
```

Use:

- xUnit for test structure;
- `Microsoft.AspNetCore.Mvc.Testing` to host the real API through `WebApplicationFactory<Program>`;
- Testcontainers for .NET with the PostgreSQL module to provision a disposable `postgres:17-alpine` database;
- existing GarageFlow HTTP patterns and request/response contracts where practical.

The E2E fixture must use an environment name other than `IntegrationTests`, such as `E2ETests`, because `AddGarageFlowDataAccess` intentionally forces InMemory when the environment is `IntegrationTests`. The E2E fixture will set:

```text
Database:Provider=Postgres
Database:AutoMigrate=true
ConnectionStrings:GarageFlow=<Testcontainers PostgreSQL connection string>
Auth:*=<known E2E test auth settings>
```

In implementation, the connection string comes from `PostgreSqlContainer.GetConnectionString()`, and the auth values come from `E2eAuthSettings`.

This keeps the existing integration suite fast and adds a separate production-like suite for provider-specific behavior.

The E2E project is intentionally not included in `GarageFlow.slnx`. The standard solution build/test commands must remain usable without Docker. E2E validation runs through the E2E project path or the dedicated local/CI automation.

## Architecture

The E2E project owns its own support code:

```text
Tests/E2E/Support/Fixtures/E2eApiFixture.cs
Tests/E2E/Support/Factories/E2eWebApplicationFactory.cs
Tests/E2E/Support/Helpers/AuthHttpClientExtensions.cs
Tests/E2E/Support/Helpers/HttpResponseAssertions.cs
Tests/E2E/Support/Helpers/E2eAuthSettings.cs
```

The fixture starts one PostgreSQL container per test class or suite fixture. Each test uses unique input values so suites do not depend on execution order. The API is started through `WebApplicationFactory`, auto-migrates into the disposable PostgreSQL database, seeds the bootstrap admin, and exposes an `HttpClient`.

Each suite is vertical and additive:

1. auth smoke;
2. auth and users;
3. customers;
4. vehicles;
5. services and inventory;
6. work orders.

Every suite has a focused command and an acceptance gate. A later suite is not added until the previous suite passes against PostgreSQL.

## Validation Policy

Every E2E test must assert the real contract, not merely that the API did not return `500`.

Examples:

- create routes assert `201 Created`, required IDs, and response fields;
- update routes assert `200 OK` and changed fields;
- delete routes assert `204 NoContent`, followed by a read returning `404`;
- duplicate resources assert `409 Conflict` and expected ProblemDetails title/status/detail;
- unauthorized requests assert `401`;
- forbidden role requests assert `403`;
- read routes assert full payload shape, including nested lists and pagination metadata.

Any test that only asserts `response.IsSuccessStatusCode` is incomplete.

## Suite Gates

### Suite 1: E2E Harness And Auth Smoke

Goal: prove that Testcontainers, PostgreSQL, migrations, API startup, bootstrap admin login, and authenticated health/API calls work.

Gate:

```bash
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~Smoke"
```

Expected: health returns `200 OK`, bootstrap admin can log in, bootstrap password change works when required.

### Suite 2: Auth And Users

Goal: validate login, password change, staff user creation, profile update, user listing, and authorization boundaries.

Gate:

```bash
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~Auth|FullyQualifiedName~Users"
```

Expected: contracts match existing API behavior and invalid credentials return expected failure contracts.

### Suite 3: Customers

Goal: validate customer CRUD, portal user activation, customer lookup with no vehicles, and customer lookup with vehicles.

Gate:

```bash
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~Customers"
```

Expected: `GET /customers/{id}` returns the full customer contract and nested vehicles when present. This suite must catch Npgsql translation issues like filtering projected vehicle details by customer.

### Suite 4: Vehicles

Goal: validate vehicle brands, models, colors, vehicles, duplicate-name contracts, case-insensitive uniqueness, and delete-blocking rules.

Gate:

```bash
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~Vehicles"
```

Expected: create/list/get/update/delete contracts pass against PostgreSQL. This suite must catch Npgsql translation issues like unsupported string APIs in repository queries.

### Suite 5: Services And Inventory

Goal: validate service CRUD, inventory CRUD, stock update, and stock-related response contracts.

Gate:

```bash
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~Services|FullyQualifiedName~Inventory"
```

Expected: response contracts and persisted effects are observable through API reads.

### Suite 6: Work Orders

Goal: validate the complete work-order journey: create customer and vehicle, create work order, create estimate, add service and inventory lines, submit, approve as customer, start work, complete services, and read work-order details/metrics.

Gate:

```bash
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~WorkOrders"
```

Expected: state transitions, nested payloads, totals, authorization, and metric contracts match the API design.

## CI And Local Automation

Add a local script that runs the E2E project:

```text
scripts/run-e2e.ps1
```

The script must:

- verify Docker is reachable;
- restore/build the solution;
- run the E2E project with xUnit parallelization constrained if necessary;
- return a non-zero exit code on any failed suite.

Add a GitHub Actions job after the existing build/unit/integration checks:

```text
e2e-real-api
```

The job runs on Ubuntu, uses Docker through the GitHub runner, restores/builds the solution, and executes the E2E project. It should upload test results when available.

## Local Seed Automation

The existing `scripts/seed-local.sql` file should become an optional startup seed step for local development. It runs after migrations and bootstrap admin creation, controlled by:

```text
Database:AutoSeed=true|false
Database:SeedScriptPath=scripts/seed-local.sql
```

`AutoSeed` defaults to `false` unless explicitly configured. The local `docker-compose.yml` sets it to true by default through:

```text
Database__AutoSeed=${DATABASE_AUTO_SEED:-true}
```

The seed step is Postgres-only. If a non-relational provider enables `AutoSeed`, the API logs that the seed was skipped. If `AutoSeed=true` and the SQL file is missing, startup fails with an actionable error because local Docker expects deterministic seed data to be available.

The Docker runtime image must copy `scripts/seed-local.sql` into `/app/scripts/seed-local.sql`, so local Compose users get migrations and seed data automatically on startup.

## Out Of Scope

- browser UI automation;
- Cypress or Playwright for now;
- performance/load testing;
- nightly scheduled monitors;
- external email delivery validation;
- testing against a shared developer database;
- rewriting the existing InMemory integration suite.

## Acceptance Criteria

- A new `Tests/E2E` project runs against PostgreSQL through Testcontainers.
- The standard `GarageFlow.slnx` test path does not require Docker.
- The E2E fixture does not use the `IntegrationTests` environment and does not fall back to InMemory.
- Each suite validates exact API contracts and failure contracts.
- Each new suite has a focused validation command before the next suite is implemented.
- Local Docker startup can apply `scripts/seed-local.sql` automatically after migrations when `Database:AutoSeed=true`.
- The E2E project can run locally through a script.
- GitHub Actions can run E2E tests in a dedicated job.
- Existing unit and integration suites remain unchanged except for shared helper moves that are explicitly planned.

## References

- Testcontainers for .NET ASP.NET Core example: https://dotnet.testcontainers.org/examples/aspnet/
- Docker Testcontainers .NET ASP.NET Core guide: https://docs.docker.com/guides/testcontainers-dotnet-aspnet-core/
- Microsoft ASP.NET Core integration testing documentation: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
- Testcontainers PostgreSQL module overview: https://testcontainers.com/modules/postgresql/
