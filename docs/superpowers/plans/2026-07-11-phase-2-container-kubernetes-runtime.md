# GarageFlow Phase 2 Container and Kubernetes Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make one immutable GarageFlow image safely run migrations, serve database-aware probes, pass a real container smoke journey, and operate on Kubernetes with two replicas and CPU/memory HPA.

**Architecture:** The Host gains an explicit one-shot migration mode while ordinary replicas start with migration disabled. A shared PostgreSQL Testcontainer and container-level smoke script prove the runtime before Kubernetes manifests compose namespace, configuration, migration Job, Deployment, Service, and HPA.

**Tech Stack:** .NET SDK 10.0.301, ASP.NET Core Health Checks, Docker 27+, PostgreSQL 17, xUnit/Testcontainers, Kubernetes 1.36 APIs, Kustomize, Metrics Server 0.8.1, Kubeconform 0.7.0, GitHub Actions.

## Global Constraints

- Begin only after functional-compliance and integration-reliability checkpoints pass; preserve their exact configuration keys.
- Pin SDK `10.0.301` in `global.json` and the Docker build image; pin the ASP.NET runtime image to `10.0.9`; keep every project at `net10.0`, Nullable enabled, and zero build warnings.
- Keep the Docker runtime non-root as UID `10001`, listening on HTTP port `8080`.
- Use the same image for the migration Job and API; migration mode is `dotnet GarageFlow.Host.dll --migrate-only`.
- Migration mode applies EF migrations and creates the configured bootstrap admin, honors `Database__AutoSeed=false`, then exits zero without listening.
- Ordinary Kubernetes pods set `Database__AutoMigrate=false` and never race migrations.
- Keep `/health`; add `/health/live` for process liveness and `/health/ready` for PostgreSQL connectivity.
- Run one PostgreSQL Testcontainer for the E2E test collection, not one per class; do not enable persistent Testcontainers reuse.
- Kubernetes namespace is `garageflow`; Deployment starts with two replicas and uses rolling update with `maxUnavailable: 0`.
- Resource requests are CPU `100m` and memory `128Mi`; limits are CPU `500m` and memory `512Mi`.
- HPA is `autoscaling/v2`, minimum 2, maximum 6, CPU target 60%, memory target 70%, with explicit demo-friendly scale behavior.
- The public Service is `LoadBalancer`, port 80 to target port 8080; synthetic lab data only.
- `secret.template.yaml` is never part of Kustomize resources and is never applied with unresolved variables.
- Pin Metrics Server to 0.8.1 and verify SHA-256 `4a672c4891902573a3ff753cece5de1bf1f55dd053403dfec39df9d1636b7ff1`; 0.8.x supports Kubernetes 1.31+.
- Pull-request CI uses no AWS credentials and never uploads secrets, Terraform state/plans, or rendered Kubernetes Secret content.
- This checkpoint validates Kubernetes statically; live EKS apply belongs to the AWS/CD checkpoint.

---

## File Structure

### Create

- `global.json` — exact SDK pin.
- `Host/Health/GarageFlowDatabaseHealthCheck.cs` — readiness-only PostgreSQL check.
- `Tests/E2E/Support/Fixtures/E2eApiCollection.cs` — one xUnit collection fixture.
- `Tests/Integration/Host/DatabaseInitializationTests.cs` — forced/conditional initialization behavior.
- `scripts/smoke-container.sh` — migrate/start/health/OpenAPI/auth/intake/status image journey with cleanup trap.
- `k8s/namespace.yaml`
- `k8s/configmap.yaml`
- `k8s/secret.template.yaml`
- `k8s/migration-job.yaml`
- `k8s/deployment.yaml`
- `k8s/service.yaml`
- `k8s/hpa.yaml`
- `k8s/kustomization.yaml`

### Modify

- `Adapters.Infrastructure/DataAccess/AutoMigrateGarageFlowExtensions.cs` — asynchronous conditional and forced initialization paths.
- `Host/Program.cs` — migration branch, health services/endpoints.
- `Host/appsettings.Production.json` — `Database.AutoMigrate=false`.
- `Dockerfile` — copy SDK/build inputs before restore and retain argument forwarding.
- `.dockerignore` — exclude delivery/infra/build-only data.
- `docker-compose.yml` — stable local image name and explicit integration defaults.
- All eight E2E test classes — replace `IClassFixture<E2eApiFixture>` with the shared collection.
- `coverlet.runsettings` — correct `Adapters.Infrastructure` exclusions.
- `.github/workflows/quality-gate.yml` — one build/test graph, image smoke, Compose and Kubernetes validation.

---

### Task 1: Pin the SDK and consolidate the E2E database fixture

**Files:**
- Create: `global.json`
- Create: `Tests/E2E/Support/Fixtures/E2eApiCollection.cs`
- Modify: all `Tests/E2E/**/*E2eTests.cs`
- Modify: `Dockerfile`

**Interfaces:**
- Produces: SDK pin used locally, in Docker, and by CI.
- Produces: `E2eApiCollection.Name` and one `E2eApiFixture` per full E2E run.

- [ ] **Step 1: Add the exact SDK file**

Create:

```json
{
  "sdk": {
    "version": "10.0.301",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Run `dotnet --version`; expected output begins with `10.0.3` and satisfies the pinned feature band.

- [ ] **Step 2: Write a shared E2E collection definition**

```csharp
namespace GarageFlow.Tests.E2E.Support.Fixtures;

[CollectionDefinition("GarageFlow E2E API", DisableParallelization = true)]
public sealed class E2eApiCollection : ICollectionFixture<E2eApiFixture>
{
    public const string Name = "GarageFlow E2E API";
}
```

Annotate Smoke, Auth, Users, Customers, Vehicles, Services, InventoryItems, WorkOrders, and the new webhook/outbox E2E classes with `[Collection(E2eApiCollection.Name)]`; keep constructor injection, remove every `IClassFixture<E2eApiFixture>`.

- [ ] **Step 3: Prove no class-level fixture remains**

```powershell
if (rg -n "IClassFixture<E2eApiFixture>" Tests/E2E) { exit 1 }
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~SmokeE2eTests|FullyQualifiedName~AuthE2eTests"
```

Expected: no grep matches and both suites PASS using one container lifecycle.

- [ ] **Step 4: Include SDK/build metadata in Docker restore cache**

Replace the floating base tags with `mcr.microsoft.com/dotnet/sdk:10.0.301` and `mcr.microsoft.com/dotnet/aspnet:10.0.9`. Before project files in the restore stage, add:

```dockerfile
COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["GarageFlow.slnx", "."]
```

Keep the exec-form `ENTRYPOINT`; Docker/Kubernetes arguments appended at runtime must reach the Host.

- [ ] **Step 5: Build and commit the deterministic harness**

```powershell
dotnet build GarageFlow.slnx -warnaserror
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~SmokeE2eTests"
git add global.json Dockerfile Tests/E2E
git commit -m "test(e2e): share one postgres fixture"
```

Expected: zero warnings/errors and PASS.

---

### Task 2: Add one-shot migration mode and truthful health probes

**Files:**
- Create: `Host/Health/GarageFlowDatabaseHealthCheck.cs`
- Create: `Tests/Integration/Host/DatabaseInitializationTests.cs`
- Modify: initialization extension, `Host/Program.cs`, Production settings

**Interfaces:**
- Produces: `MigrateGarageFlowAsync(...)` forced initialization and `AutoMigrateGarageFlowAsync(...)` conditional startup initialization.
- Produces: `--migrate-only`, `/health/live`, and `/health/ready`.

- [ ] **Step 1: Add failing initialization and readiness tests**

Cover:

- conditional startup skips migrations when `Database:AutoMigrate=false`;
- forced migration ignores `AutoMigrate=false`, applies migrations/EnsureCreated, and creates the bootstrap admin;
- forced migration with `Database:AutoSeed=false` does not execute the seed script;
- `/health/live` is healthy without a database check;
- `/health/ready` is healthy with the configured test database and unhealthy when connectivity is deliberately broken.

- [ ] **Step 2: Run the focused tests and observe missing APIs**

```powershell
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~DatabaseInitializationTests|FullyQualifiedName~ApiSurfaceTests"
```

Expected: FAIL because migration mode/readiness endpoints do not exist.

- [ ] **Step 3: Split forced and conditional database initialization**

Expose asynchronous extension methods:

```csharp
public static Task<IServiceProvider> AutoMigrateGarageFlowAsync(
    this IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    string contentRootPath,
    CancellationToken cancellationToken = default);

public static Task<IServiceProvider> MigrateGarageFlowAsync(
    this IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    string contentRootPath,
    CancellationToken cancellationToken = default);
```

The first checks `Database:AutoMigrate`; the second always runs relational `MigrateAsync` or InMemory `EnsureCreated`, creates bootstrap admin, and invokes existing seed logic—which exits immediately when `Database:AutoSeed=false`.

- [ ] **Step 4: Add a database readiness check in Host**

```csharp
public sealed class GarageFlowDatabaseHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();

        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("GarageFlow database is unavailable.");
    }
}
```

Register it with tag `ready`. Map `/health/live` with a predicate that runs no checks and `/health/ready` with `registration.Tags.Contains("ready")`; preserve the existing JSON `/health` endpoint.

- [ ] **Step 5: Branch into migration mode before mapping/listening**

Immediately after `builder.Build()`:

```csharp
if (args.Contains("--migrate-only", StringComparer.Ordinal))
{
    await app.Services.MigrateGarageFlowAsync(
        app.Configuration,
        app.Environment,
        app.Environment.ContentRootPath);
    return;
}

await app.Services.AutoMigrateGarageFlowAsync(
    app.Configuration,
    app.Environment,
    app.Environment.ContentRootPath);
```

Set `Database.AutoMigrate` to `false` in `Host/appsettings.Production.json`.

- [ ] **Step 6: Run integration tests and direct migration process**

```powershell
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~DatabaseInitializationTests|FullyQualifiedName~ApiSurfaceTests"
$env:ASPNETCORE_ENVIRONMENT = "IntegrationTests"
$env:Database__Provider = "InMemory"
$env:Database__DatabaseName = "garageflow-migrate-only-plan"
try {
    dotnet run --project Host/GarageFlow.Host.csproj -- --migrate-only
    if ($LASTEXITCODE -ne 0) { throw "Migration process exited with code $LASTEXITCODE." }
}
finally {
    Remove-Item Env:ASPNETCORE_ENVIRONMENT, Env:Database__Provider, Env:Database__DatabaseName
}
```

Expected: tests PASS; the second command exits successfully after initializing the disposable InMemory database and never prints a listening URL.

- [ ] **Step 7: Commit runtime initialization**

```powershell
git add Host Adapters.Infrastructure/DataAccess Tests/Integration
git commit -m "feat(host): add one-shot database migration mode"
```

---

### Task 3: Prove the built image against PostgreSQL

**Files:**
- Create: `scripts/smoke-container.sh`
- Modify: `Dockerfile`, `.dockerignore`, `docker-compose.yml`

**Interfaces:**
- Consumes: complete intake, status, auth, probes, and migration mode.
- Produces: a noninteractive image smoke command with guaranteed cleanup.

- [ ] **Step 1: Harden the build context and Compose defaults**

Add these build-context exclusions without excluding runtime `scripts/seed-local.sql`:

```text
.github/
infra/
k8s/
scripts/load-test/
**/.terraform/
**/*.tfstate
**/*.tfstate.*
```

Set the Compose API image to `garageflow-api:local`; add a 32+ character local webhook HMAC secret, `Integrations__Outbox__Enabled=false`, and `Integrations__Sns__Region=us-east-1`. Preserve local automatic migration/seed behavior.

- [ ] **Step 2: Write the smoke script with an unconditional cleanup trap**

The script accepts one image argument, creates names suffixed by the current PID, and starts `postgres:17-alpine` on an isolated network. Its required skeleton is:

```bash
#!/usr/bin/env bash
set -Eeuo pipefail

IMAGE="${1:?usage: smoke-container.sh <image>}"
SUFFIX="$$"
NETWORK="garageflow-smoke-${SUFFIX}"
POSTGRES="garageflow-postgres-${SUFFIX}"
API="garageflow-api-${SUFFIX}"

cleanup() {
  docker rm -f "${API}" "${POSTGRES}" >/dev/null 2>&1 || true
  docker network rm "${NETWORK}" >/dev/null 2>&1 || true
}
trap cleanup EXIT
```

Use fixed synthetic bootstrap values in local variables but never echo passwords/tokens.

- [ ] **Step 3: Run migration and API from the exact same image**

The script must:

1. wait for `pg_isready`;
2. `docker run --rm ... "$IMAGE" --migrate-only` with `AutoMigrate=false`, `AutoSeed=false`;
3. start the image detached with `AutoMigrate=false`, outbox disabled, mapped to a random host port;
4. poll `/health/ready` with a bounded 120-second timeout;
5. check `/health`, `/health/live`, and `/openapi/v1.json`;
6. log in as bootstrap admin, change the mandatory first password, and log in again;
7. send complete intake with a generated UUID, valid CPF `52998224725`, phone `+5511999999999`, one service, and no inventory;
8. verify `201`, `Location`, returned status `Received`, and `GET /work-orders/{id}/status`;
9. fail with API logs on any error; always execute cleanup.

- [ ] **Step 4: Build and execute the image smoke**

```powershell
docker build -t garageflow:phase2-runtime .
bash scripts/smoke-container.sh garageflow:phase2-runtime
docker compose config --quiet
```

Expected: all checks pass; `docker ps -a --filter name=garageflow-smoke` and `docker network ls --filter name=garageflow-smoke` show no leftovers.

- [ ] **Step 5: Commit the container contract**

```powershell
git add Dockerfile .dockerignore docker-compose.yml scripts/smoke-container.sh
git commit -m "test(container): verify migration and api image journey"
```

---

### Task 4: Add Kubernetes workload manifests

**Files:**
- Create: all eight root `k8s` manifests listed above

**Interfaces:**
- Consumes: image port 8080, migration mode, health endpoints, integration settings.
- Produces: Kustomize-renderable workload excluding the Secret template.

- [ ] **Step 1: Create namespace and static ConfigMap**

Use namespace `garageflow` and ConfigMap `garageflow-config`. Its data includes:

```yaml
ASPNETCORE_ENVIRONMENT: Production
Database__Provider: Postgres
Database__AutoMigrate: "false"
Database__AutoSeed: "false"
Auth__Jwt__Issuer: GarageFlow
Auth__Jwt__Audience: GarageFlow.Adapters.Api
Auth__Jwt__ExpiresMinutes: "120"
Auth__BootstrapAdmin__FullName: GarageFlow Academy Admin
Auth__BootstrapAdmin__BirthDate: "1990-01-01"
Integrations__Outbox__Enabled: "true"
Integrations__Outbox__BatchSize: "10"
Integrations__Outbox__PollingIntervalSeconds: "5"
Integrations__Outbox__LeaseDurationSeconds: "300"
Integrations__Outbox__InitialRetryDelaySeconds: "5"
Integrations__Outbox__MaxRetryDelaySeconds: "300"
Integrations__Sns__Region: us-east-1
Integrations__Sns__TopicArn: ""
```

The deploy workflow patches the empty topic ARN before any Job/Pod starts.

- [ ] **Step 2: Create a Secret template that cannot be applied accidentally**

Name it `garageflow-secrets`, use `stringData`, and include shell-variable markers for connection string, JWT key, bootstrap admin email/password, webhook HMAC secret, and temporary AWS access key/secret/session token. The non-sensitive bootstrap full name and birth date stay in `garageflow-config`. Add an annotation `garageflow.io/template-only: "true"`. Do not include this file in `kustomization.yaml`.

- [ ] **Step 3: Create the migration Job**

Use image `garageflow-api:local`, `args: ["--migrate-only"]`, `envFrom` ConfigMap/Secret, `restartPolicy: Never`, `backoffLimit: 1`, `activeDeadlineSeconds: 600`, and the same non-root security context as the app. Give the Job a fixed name `garageflow-migration`; deployment must delete an old Job before changing its immutable pod template.

- [ ] **Step 4: Create the hardened Deployment**

Required fields:

```yaml
replicas: 2
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxUnavailable: 0
    maxSurge: 1
template:
  spec:
    terminationGracePeriodSeconds: 30
    securityContext:
      runAsNonRoot: true
      runAsUser: 10001
      runAsGroup: 10001
      seccompProfile:
        type: RuntimeDefault
```

Container security drops all capabilities, disallows privilege escalation, uses a read-only root filesystem plus an `emptyDir` mounted at `/tmp`, exposes `http: 8080`, loads ConfigMap/Secret, and declares exact resources from Global Constraints.

Probes:

```yaml
startupProbe:  { httpGet: { path: /health/live,  port: http }, periodSeconds: 5, failureThreshold: 30 }
livenessProbe: { httpGet: { path: /health/live,  port: http }, periodSeconds: 10, failureThreshold: 3 }
readinessProbe:{ httpGet: { path: /health/ready, port: http }, periodSeconds: 5, failureThreshold: 3 }
```

Format these as valid expanded YAML; the inline line above only fixes the values.

- [ ] **Step 5: Create Service and HPA**

Service selects `app.kubernetes.io/name: garageflow-api`, uses type `LoadBalancer`, port 80, target `http`. HPA targets Deployment `garageflow-api`, min 2/max 6, CPU utilization 60 and memory 70. Configure immediate scale-up (max 100% or 2 pods per 15 seconds) and conservative scale-down (60-second stabilization, max 1 pod per 30 seconds).

- [ ] **Step 6: Compose only safe resources with Kustomize**

`kustomization.yaml` uses `apiVersion: kustomize.config.k8s.io/v1beta1`, namespace `garageflow`, and resources:

```text
namespace.yaml
configmap.yaml
migration-job.yaml
deployment.yaml
service.yaml
hpa.yaml
```

It deliberately excludes `secret.template.yaml`.

- [ ] **Step 7: Render and validate manifests**

```bash
rendered="$(mktemp)"
trap 'rm -f "${rendered}"' EXIT
kubectl kustomize k8s > "${rendered}"
kubectl apply --dry-run=client --validate=false -f "${rendered}"
docker run --rm -i ghcr.io/yannh/kubeconform:v0.7.0 \
  -strict -summary -kubernetes-version 1.36.0 -ignore-missing-schemas - < "${rendered}"
```

Expected: every rendered resource valid; Secret template absent from output.

- [ ] **Step 8: Commit manifests**

```powershell
git add k8s
git commit -m "feat(k8s): add GarageFlow runtime manifests"
```

---

### Task 5: Consolidate CI and validate the runtime artifacts

**Files:**
- Modify: `.github/workflows/quality-gate.yml`
- Modify: `coverlet.runsettings`

**Interfaces:**
- Produces: one pull-request quality job with code, tests, coverage, image smoke, Compose, and Kubernetes gates.

- [ ] **Step 1: Correct coverage paths and remove duplicate execution**

Change exclusions from `**/Infrastructure/DataAccess/...` to `**/Adapters.Infrastructure/DataAccess/...`. Remove the second `e2e-real-api` job and redundant E2E restore/build. Keep the optional Sonar setup and aggregate 80% script.

- [ ] **Step 2: Define one deterministic .NET test graph**

Use `actions/setup-dotnet@v5` with `global-json-file: global.json`, then:

```text
dotnet restore GarageFlow.slnx
dotnet build GarageFlow.slnx --no-restore -warnaserror
architecture tests with FullyQualifiedName~Architecture
unit tests with FullyQualifiedName!~Architecture
integration tests once, MaxCpuCount=1
E2E tests once
```

Collect coverage for architecture/unit/integration, upload the single E2E TRX on `always()`, and preserve the 80% aggregate threshold.

- [ ] **Step 3: Add container and Compose gates**

```bash
docker build -t "garageflow:${GITHUB_SHA}" .
bash scripts/smoke-container.sh "garageflow:${GITHUB_SHA}"
docker compose config --quiet
```

Do not rebuild inside the smoke script.

- [ ] **Step 4: Add static Kubernetes validation**

Render `kubectl kustomize k8s` to standard input and validate with `ghcr.io/yannh/kubeconform:v0.7.0`, strict mode, summary, Kubernetes `1.36.0`, and missing-schema ignore only for APIs unavailable in the bundled schema. Assert rendered output does not contain `template-only` or shell-secret variable markers.

- [ ] **Step 5: Validate workflow syntax locally**

```powershell
docker run --rm -v "${PWD}:/repo" -w /repo rhysd/actionlint:1.7.12
```

Expected: no workflow diagnostics.

- [ ] **Step 6: Run the complete local quality equivalent**

```powershell
dotnet build GarageFlow.slnx -warnaserror
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
docker build -t garageflow:quality-local .
bash scripts/smoke-container.sh garageflow:quality-local
docker compose config --quiet
```

Expected: all PASS with Docker running.

- [ ] **Step 7: Commit the quality gate**

```powershell
git add .github/workflows/quality-gate.yml coverlet.runsettings
git commit -m "ci: validate GarageFlow container and kubernetes runtime"
```

---

## Checkpoint Acceptance

The container/Kubernetes checkpoint is complete only when:

- SDK selection is deterministic locally, in CI, and in Docker;
- the full E2E suite owns one temporary PostgreSQL container;
- migration mode initializes schema/admin and exits without starting the API;
- liveness is process-only and readiness fails when PostgreSQL is unavailable;
- the exact built image passes migration, probes, OpenAPI, auth, intake, and status smoke checks;
- Kubernetes renders two secure replicas, one migration Job, public Service, and exact CPU/memory HPA;
- the Secret template cannot appear in Kustomize output;
- CI runs E2E once, enforces 80% coverage, validates the built image, Compose, and Kubernetes;
- no AWS credentials are required and all checkpoint commands pass.
