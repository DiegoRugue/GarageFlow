# Clean Architecture DDD Modular Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate GarageFlow to the approved Clean Architecture + DDD modular structure with explicit `Host`, `Adapters.Api`, `Adapters.Infrastructure`, `Application`, `Domain`, and `SharedKernel` boundaries.

**Architecture:** The migration is phase-based and keeps every phase buildable. Mechanical renames happen before behavior changes. Domain purity is achieved by moving ports/read models into Application, then splitting query ports from mutation repositories, then hardening API contracts and cross-cutting behaviors.

**Tech Stack:** C#/.NET 10, ASP.NET Core Minimal APIs, Mediator 3.0, EF Core 10, Npgsql, xUnit, Moq, NetArchTest, Testcontainers PostgreSQL.

---

## Source Spec

Use this spec as the source of truth:

```text
docs/superpowers/specs/2026-07-04-clean-architecture-ddd-modular-migration-design.md
```

## Execution Rules

- Start from a clean worktree.
- Use PowerShell commands in this plan from `C:\projects\GarageFlow`.
- Keep each task as a separate commit.
- Do not mix behavior refactors into mechanical rename tasks.
- Run the focused verification listed in each task before committing.
- If a command fails because a file was already moved in the current task, inspect with `git status --short` and continue only when the intended file exists in the target path.

## Target Physical Structure

The final physical folders should be:

```text
SharedKernel/
Domain/
Application/
Adapters.Api/
Adapters.Infrastructure/
Host/
Tests/
```

The final production projects should be:

```text
SharedKernel/GarageFlow.SharedKernel.csproj
Domain/GarageFlow.Domain.csproj
Application/GarageFlow.Application.csproj
Adapters.Api/GarageFlow.Adapters.Api.csproj
Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj
Host/GarageFlow.Host.csproj
```

## Main File Movement Map

```text
BuildingBlocks/* -> SharedKernel/*
Api/* -> Adapters.Api/*
Infrastructure/* -> Adapters.Infrastructure/*
Adapters.Api/Program.cs -> Host/Program.cs
Adapters.Api/appsettings*.json -> Host/appsettings*.json
Adapters.Api/Properties/launchSettings.json -> Host/Properties/launchSettings.json
Adapters.Api/Middlewares/ExceptionHandlingExtensions.cs -> Host/Middlewares/ExceptionHandlingExtensions.cs
Domain/<Module>/Repositories/I*Repository.cs -> Application/<Module>/Ports/I*Repository.cs
Domain/<Module>/Repositories/*ReadModel.cs -> Application/<Module>/ReadModels/*ReadModel.cs
Application/<Module>/<UseCase>/* -> Application/<Module>/UseCases/<UseCase>/*
```

## Task 1: Baseline, E2E Solution Inclusion, And Safety Checks

**Files:**
- Modify: `GarageFlow.slnx`
- Verify: `Tests/E2E/GarageFlow.Tests.E2E.csproj`

- [ ] **Step 1: Confirm a clean worktree**

Run:

```powershell
git status --short
```

Expected: no output.

- [ ] **Step 2: Run current architecture tests**

Run:

```powershell
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture"
```

Expected: PASS with architecture and module convention tests green.

- [ ] **Step 3: Add E2E project to the solution**

Edit `GarageFlow.slnx` so the `/Tests/` section includes:

```xml
  <Folder Name="/Tests/E2E/">
    <Project Path="Tests/E2E/GarageFlow.Tests.E2E.csproj" />
  </Folder>
```

The relevant section should become:

```xml
  <Folder Name="/Tests/" />
  <Folder Name="/Tests/E2E/">
    <Project Path="Tests/E2E/GarageFlow.Tests.E2E.csproj" />
  </Folder>
  <Folder Name="/Tests/Integration/">
    <Project Path="Tests/Integration/GarageFlow.Tests.Integration.csproj" />
  </Folder>
```

- [ ] **Step 4: Verify solution restore and build**

Run:

```powershell
dotnet build GarageFlow.slnx
```

Expected: PASS.

- [ ] **Step 5: Commit baseline solution change**

Run:

```powershell
git add GarageFlow.slnx
git commit -m "chore: include e2e project in solution"
```

Expected: commit succeeds.

## Task 2: Rename BuildingBlocks To SharedKernel

**Files:**
- Move: `BuildingBlocks/` to `SharedKernel/`
- Rename: `SharedKernel/GarageFlow.BuildingBlocks.csproj` to `SharedKernel/GarageFlow.SharedKernel.csproj`
- Modify: `Domain/GarageFlow.Domain.csproj`
- Modify: `Application/GarageFlow.Application.csproj`
- Modify: `Adapters.Infrastructure` paths only if Task 3 has already run in the current branch
- Modify: `Tests/Shared/GarageFlow.Tests.Shared.csproj`
- Modify: `Tests/Unit/Architecture/DependencyRulesTests.cs`
- Modify: `GarageFlow.slnx`

- [ ] **Step 1: Move the project folder and project file**

Run:

```powershell
Move-Item -LiteralPath BuildingBlocks -Destination SharedKernel
Rename-Item -LiteralPath SharedKernel\GarageFlow.BuildingBlocks.csproj -NewName GarageFlow.SharedKernel.csproj
```

Expected: `SharedKernel\GarageFlow.SharedKernel.csproj` exists.

- [ ] **Step 2: Replace assembly and namespace names**

Run:

```powershell
$paths = @(
    "SharedKernel",
    "Domain",
    "Application",
    "Infrastructure",
    "Api",
    "Tests",
    "GarageFlow.slnx",
    "Dockerfile",
    "README.md",
    "AGENTS.md"
)

$files = foreach ($path in $paths) {
    if (Test-Path -LiteralPath $path) {
        Get-ChildItem -LiteralPath $path -Recurse -File -Include *.cs,*.csproj,*.slnx,*.md,*.yml,*.json,Dockerfile |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' }
    }
}

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $content = $content.Replace("GarageFlow.BuildingBlocks", "GarageFlow.SharedKernel")
    $content = $content.Replace("BuildingBlocks", "SharedKernel")
    Set-Content -LiteralPath $file.FullName -Value $content -NoNewline
}
```

Expected: no `GarageFlow.BuildingBlocks` references remain outside `bin`, `obj`, `.git`, `.vs`, `.worktrees`, and historical docs.

- [ ] **Step 3: Verify no active BuildingBlocks references remain**

Run:

```powershell
rg -n "GarageFlow\.BuildingBlocks|BuildingBlocks\\GarageFlow\.BuildingBlocks|/BuildingBlocks/" -g "!bin" -g "!obj" -g "!.git" -g "!.vs" -g "!.worktrees" -g "!TestResults"
```

Expected: output may include historical docs under `docs/superpowers`, but no production project, test project, Dockerfile, AGENTS, README, or solution reference should appear.

- [ ] **Step 4: Run focused validation**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture"
```

Expected: both commands PASS.

- [ ] **Step 5: Commit SharedKernel rename**

Run:

```powershell
git add SharedKernel Domain Application Infrastructure Api Tests GarageFlow.slnx Dockerfile README.md AGENTS.md
git add -u BuildingBlocks
git commit -m "refactor: rename building blocks to shared kernel"
```

Expected: commit succeeds.

## Task 3: Rename Api And Infrastructure To Adapters

**Files:**
- Move: `Api/` to `Adapters.Api/`
- Move: `Infrastructure/` to `Adapters.Infrastructure/`
- Rename: `Adapters.Api/GarageFlow.Api.csproj` to `Adapters.Api/GarageFlow.Adapters.Api.csproj`
- Rename: `Adapters.Infrastructure/GarageFlow.Infrastructure.csproj` to `Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj`
- Modify: all project references
- Modify: `GarageFlow.slnx`
- Modify: `Dockerfile`
- Modify: `Tests/Unit/Architecture/DependencyRulesTests.cs`
- Modify: `Tests/Unit/Architecture/ModuleConventionTests.cs`

- [ ] **Step 1: Move adapter folders and project files**

Run:

```powershell
Move-Item -LiteralPath Api -Destination Adapters.Api
Move-Item -LiteralPath Infrastructure -Destination Adapters.Infrastructure
Rename-Item -LiteralPath Adapters.Api\GarageFlow.Api.csproj -NewName GarageFlow.Adapters.Api.csproj
Rename-Item -LiteralPath Adapters.Infrastructure\GarageFlow.Infrastructure.csproj -NewName GarageFlow.Adapters.Infrastructure.csproj
```

Expected: both adapter project files exist.

- [ ] **Step 2: Replace namespaces and project references**

Run:

```powershell
$paths = @(
    "Adapters.Api",
    "Adapters.Infrastructure",
    "Application",
    "Domain",
    "SharedKernel",
    "Tests",
    "GarageFlow.slnx",
    "Dockerfile",
    "README.md",
    "AGENTS.md",
    "docker-compose.yml"
)

$files = foreach ($path in $paths) {
    if (Test-Path -LiteralPath $path) {
        Get-ChildItem -LiteralPath $path -Recurse -File -Include *.cs,*.csproj,*.slnx,*.md,*.yml,*.json,Dockerfile |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' }
    }
}

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $content = $content.Replace("GarageFlow.Api", "GarageFlow.Adapters.Api")
    $content = $content.Replace("GarageFlow.Infrastructure", "GarageFlow.Adapters.Infrastructure")
    $content = $content.Replace("..\Infrastructure\GarageFlow.Infrastructure.csproj", "..\Adapters.Infrastructure\GarageFlow.Adapters.Infrastructure.csproj")
    $content = $content.Replace("..\..\Infrastructure\GarageFlow.Infrastructure.csproj", "..\..\Adapters.Infrastructure\GarageFlow.Adapters.Infrastructure.csproj")
    $content = $content.Replace("..\..\Api\GarageFlow.Api.csproj", "..\..\Adapters.Api\GarageFlow.Adapters.Api.csproj")
    $content = $content.Replace("Api/GarageFlow.Api.csproj", "Adapters.Api/GarageFlow.Adapters.Api.csproj")
    $content = $content.Replace("Infrastructure/GarageFlow.Infrastructure.csproj", "Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj")
    Set-Content -LiteralPath $file.FullName -Value $content -NoNewline
}
```

Expected: production and test code use `GarageFlow.Adapters.Api` and `GarageFlow.Adapters.Infrastructure` namespaces.

- [ ] **Step 3: Update architecture test constants**

Edit `Tests/Unit/Architecture/DependencyRulesTests.cs` so the constants at the top are:

```csharp
private const string ApiNamespace = "GarageFlow.Adapters.Api";
private const string ApplicationNamespace = "GarageFlow.Application";
private const string SharedKernelNamespace = "GarageFlow.SharedKernel";
private const string DomainNamespace = "GarageFlow.Domain";
private const string InfrastructureNamespace = "GarageFlow.Adapters.Infrastructure";
```

Then replace the old `BuildingBlocksNamespace` identifier in that file with `SharedKernelNamespace`.

- [ ] **Step 4: Update module convention layer paths**

Edit `Tests/Unit/Architecture/ModuleConventionTests.cs` so module path checks use adapter folders:

```csharp
Path.Combine(RepositoryRoot, "Adapters.Api", module),
Path.Combine(RepositoryRoot, "Application", module),
Path.Combine(RepositoryRoot, "Domain", module),
Path.Combine(RepositoryRoot, "Adapters.Infrastructure", module),
```

In the same file, replace calls to `GetModuleFiles("Api", missingPaths)` with:

```csharp
GetModuleFiles("Adapters.Api", missingPaths)
```

Replace calls to `GetModuleFiles("Infrastructure", missingPaths)` or hard-coded infrastructure folder checks with `Adapters.Infrastructure`.

- [ ] **Step 5: Verify no active old adapter references remain**

Run:

```powershell
rg -n "GarageFlow\.Api|GarageFlow\.Infrastructure|Api\\GarageFlow\.Api|Infrastructure\\GarageFlow\.Infrastructure" -g "!bin" -g "!obj" -g "!.git" -g "!.vs" -g "!.worktrees" -g "!TestResults"
```

Expected: output may include historical docs under `docs/superpowers`, but no active project, test, Docker, README, AGENTS, or solution reference should appear.

- [ ] **Step 6: Run validation**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture"
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~Smoke"
```

Expected: all commands PASS.

- [ ] **Step 7: Commit adapter rename**

Run:

```powershell
git add Adapters.Api Adapters.Infrastructure Application Domain SharedKernel Tests GarageFlow.slnx Dockerfile README.md AGENTS.md docker-compose.yml
git add -u Api Infrastructure
git commit -m "refactor: rename api and infrastructure adapters"
```

Expected: commit succeeds.

## Task 4: Introduce Host As Composition Root

**Files:**
- Create: `Host/GarageFlow.Host.csproj`
- Move: `Adapters.Api/Program.cs` to `Host/Program.cs`
- Move: `Adapters.Api/appsettings.json` to `Host/appsettings.json`
- Move: `Adapters.Api/appsettings.Development.json` to `Host/appsettings.Development.json`
- Move: `Adapters.Api/appsettings.Production.json` to `Host/appsettings.Production.json`
- Move: `Adapters.Api/Properties/launchSettings.json` to `Host/Properties/launchSettings.json`
- Move: `Adapters.Api/Middlewares/ExceptionHandlingExtensions.cs` to `Host/Middlewares/ExceptionHandlingExtensions.cs`
- Modify: `Adapters.Api/GarageFlow.Adapters.Api.csproj`
- Modify: `Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj`
- Modify: `Adapters.Infrastructure/DataAccess/DependencyInjection.cs`
- Modify: `Adapters.Infrastructure/DataAccess/AutoMigrateGarageFlowExtensions.cs`
- Modify: `Dockerfile`
- Modify: `GarageFlow.slnx`
- Modify: `Tests/Integration/GarageFlow.Tests.Integration.csproj`
- Modify: `Tests/E2E/GarageFlow.Tests.E2E.csproj`

- [ ] **Step 1: Create Host project and move startup files**

Run:

```powershell
New-Item -ItemType Directory -Force -Path Host,Host\Properties,Host\Middlewares
Move-Item -LiteralPath Adapters.Api\Program.cs -Destination Host\Program.cs
Move-Item -LiteralPath Adapters.Api\appsettings.json -Destination Host\appsettings.json
Move-Item -LiteralPath Adapters.Api\appsettings.Development.json -Destination Host\appsettings.Development.json
Move-Item -LiteralPath Adapters.Api\appsettings.Production.json -Destination Host\appsettings.Production.json
Move-Item -LiteralPath Adapters.Api\Properties\launchSettings.json -Destination Host\Properties\launchSettings.json
Move-Item -LiteralPath Adapters.Api\Middlewares\ExceptionHandlingExtensions.cs -Destination Host\Middlewares\ExceptionHandlingExtensions.cs
```

Expected: `Host\Program.cs` and `Host\GarageFlow.Host.csproj` are ready to create.

- [ ] **Step 2: Create Host csproj**

Create `Host/GarageFlow.Host.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <ProjectReference Include="..\Adapters.Api\GarageFlow.Adapters.Api.csproj" />
    <ProjectReference Include="..\Adapters.Infrastructure\GarageFlow.Adapters.Infrastructure.csproj" />
    <ProjectReference Include="..\Application\GarageFlow.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.7" />
    <PackageReference Include="Mediator.SourceGenerator" Version="3.0.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Scalar.AspNetCore" Version="2.14.4" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

- [ ] **Step 3: Convert API adapter project to class library**

Replace `Adapters.Api/GarageFlow.Adapters.Api.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\Application\GarageFlow.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.7" />
    <PackageReference Include="Mediator.Abstractions" Version="3.0.*" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

- [ ] **Step 4: Update Host Program imports and public Program type**

Edit `Host/Program.cs` so top-level usings include:

```csharp
using GarageFlow.Adapters.Api.Auth;
using GarageFlow.Adapters.Api.Customers;
using GarageFlow.Adapters.Api.InventoryItems;
using GarageFlow.Adapters.Api.Security;
using GarageFlow.Adapters.Api.Services;
using GarageFlow.Adapters.Api.Users;
using GarageFlow.Adapters.Api.Vehicles;
using GarageFlow.Adapters.Api.WorkOrders;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Host.Middlewares;
using Scalar.AspNetCore;
```

Remove the old API middleware using. At the end of `Host/Program.cs`, add:

```csharp
public partial class Program;
```

- [ ] **Step 5: Move exception handler namespace to Host**

Edit `Host/Middlewares/ExceptionHandlingExtensions.cs` so the namespace is:

```csharp
namespace GarageFlow.Host.Middlewares;
```

Keep its `using GarageFlow.SharedKernel.Domain.Exceptions;` import because Host may map application and shared-kernel exceptions to HTTP.

- [ ] **Step 6: Replace infrastructure service registration signature**

Edit `Adapters.Infrastructure/DataAccess/DependencyInjection.cs`.

Replace:

```csharp
public static WebApplicationBuilder AddGarageFlowDataAccess(this WebApplicationBuilder builder)
```

with:

```csharp
public static IServiceCollection AddGarageFlowInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
```

Inside that method, replace `builder.Configuration` with `configuration`, `builder.Environment` with `environment`, and `builder.Services` with `services`. The method must end with:

```csharp
return services;
```

The registration calls must use `services.AddScoped(...)`.

- [ ] **Step 7: Replace auto-migration signature**

Edit `Adapters.Infrastructure/DataAccess/AutoMigrateGarageFlowExtensions.cs`.

Replace:

```csharp
public static WebApplication AutoMigrateGarageFlow(this WebApplication app)
```

with:

```csharp
public static IServiceProvider AutoMigrateGarageFlow(
    this IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    string contentRootPath)
```

Inside the method, replace `app.Configuration` with `configuration`, `app.Environment` with `environment`, and `app.Services.CreateScope()` with `serviceProvider.CreateScope()`.

Change `ApplyLocalSeed` signature to:

```csharp
private static void ApplyLocalSeed(
    IConfiguration configuration,
    IHostEnvironment environment,
    GarageFlowDbContext dbContext,
    ILogger logger,
    string contentRootPath)
```

Inside `ApplyLocalSeed`, replace `app.Configuration` with `configuration`, `app.Environment` with `environment`, and `app.Environment.ContentRootPath` with `contentRootPath`.

The public method must end with:

```csharp
return serviceProvider;
```

- [ ] **Step 8: Update Host Program registration calls**

In `Host/Program.cs`, replace:

```csharp
builder.AddGarageFlowDataAccess();
```

with:

```csharp
builder.Services.AddGarageFlowInfrastructure(builder.Configuration, builder.Environment);
```

Replace:

```csharp
app.AutoMigrateGarageFlow();
```

with:

```csharp
app.Services.AutoMigrateGarageFlow(app.Configuration, app.Environment, app.Environment.ContentRootPath);
```

- [ ] **Step 9: Update solution and test project references**

Edit `GarageFlow.slnx` so it contains:

```xml
  <Folder Name="/Adapters.Api/">
    <Project Path="Adapters.Api/GarageFlow.Adapters.Api.csproj" />
  </Folder>
  <Folder Name="/Adapters.Infrastructure/">
    <Project Path="Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj" />
  </Folder>
  <Folder Name="/Host/">
    <Project Path="Host/GarageFlow.Host.csproj" />
  </Folder>
```

Edit both `Tests/Integration/GarageFlow.Tests.Integration.csproj` and `Tests/E2E/GarageFlow.Tests.E2E.csproj` to reference Host instead of the API adapter:

```xml
<ProjectReference Include="..\..\Host\GarageFlow.Host.csproj" />
```

The integration test project should keep its infrastructure adapter reference:

```xml
<ProjectReference Include="..\..\Adapters.Infrastructure\GarageFlow.Adapters.Infrastructure.csproj" />
```

- [ ] **Step 10: Update Dockerfile**

Replace Dockerfile project copy/restore/build/publish entries so the executable is Host:

```dockerfile
COPY ["Host/GarageFlow.Host.csproj", "Host/"]
COPY ["Adapters.Api/GarageFlow.Adapters.Api.csproj", "Adapters.Api/"]
COPY ["Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj", "Adapters.Infrastructure/"]
COPY ["Application/GarageFlow.Application.csproj", "Application/"]
COPY ["Domain/GarageFlow.Domain.csproj", "Domain/"]
COPY ["SharedKernel/GarageFlow.SharedKernel.csproj", "SharedKernel/"]

RUN dotnet restore "Host/GarageFlow.Host.csproj"
```

Replace build and publish commands:

```dockerfile
RUN dotnet build "Host/GarageFlow.Host.csproj" -c Release --no-restore
RUN dotnet publish "Host/GarageFlow.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-build
```

Replace entrypoint:

```dockerfile
ENTRYPOINT ["dotnet", "GarageFlow.Host.dll"]
```

- [ ] **Step 11: Run Host validation**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture"
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~Smoke"
```

Expected: all commands PASS.

- [ ] **Step 12: Commit Host extraction**

Run:

```powershell
git add Host Adapters.Api Adapters.Infrastructure Tests GarageFlow.slnx Dockerfile
git add -u Adapters.Api
git commit -m "refactor: introduce host composition root"
```

Expected: commit succeeds.

## Task 5: Move Application Use Cases Under UseCases

**Files:**
- Move: `Application/<Module>/<UseCase>/` to `Application/<Module>/UseCases/<UseCase>/`
- Move: `Application/Auth/Login/` to `Application/Auth/UseCases/Login/`
- Modify: namespaces and usings across `Application`, `Adapters.Api`, and `Tests`
- Modify: `Tests/Unit/Architecture/ModuleConventionTests.cs`

- [ ] **Step 1: Move use-case folders**

Run:

```powershell
$moves = @(
    @{ From = "Application\Auth\Login"; To = "Application\Auth\UseCases\Login" },
    @{ From = "Application\Customers\ActivateCustomerPortalUser"; To = "Application\Customers\UseCases\ActivateCustomerPortalUser" },
    @{ From = "Application\Customers\CreateCustomer"; To = "Application\Customers\UseCases\CreateCustomer" },
    @{ From = "Application\Customers\DeleteCustomer"; To = "Application\Customers\UseCases\DeleteCustomer" },
    @{ From = "Application\Customers\GetCustomerById"; To = "Application\Customers\UseCases\GetCustomerById" },
    @{ From = "Application\Customers\ListCustomers"; To = "Application\Customers\UseCases\ListCustomers" },
    @{ From = "Application\Customers\UpdateCustomer"; To = "Application\Customers\UseCases\UpdateCustomer" },
    @{ From = "Application\InventoryItems\CreateInventoryItem"; To = "Application\InventoryItems\UseCases\CreateInventoryItem" },
    @{ From = "Application\InventoryItems\DeleteInventoryItem"; To = "Application\InventoryItems\UseCases\DeleteInventoryItem" },
    @{ From = "Application\InventoryItems\GetInventoryItemById"; To = "Application\InventoryItems\UseCases\GetInventoryItemById" },
    @{ From = "Application\InventoryItems\ListInventoryItems"; To = "Application\InventoryItems\UseCases\ListInventoryItems" },
    @{ From = "Application\InventoryItems\UpdateInventoryItem"; To = "Application\InventoryItems\UseCases\UpdateInventoryItem" },
    @{ From = "Application\InventoryItems\UpdateInventoryItemStock"; To = "Application\InventoryItems\UseCases\UpdateInventoryItemStock" },
    @{ From = "Application\Services\CreateService"; To = "Application\Services\UseCases\CreateService" },
    @{ From = "Application\Services\DeleteService"; To = "Application\Services\UseCases\DeleteService" },
    @{ From = "Application\Services\GetServiceById"; To = "Application\Services\UseCases\GetServiceById" },
    @{ From = "Application\Services\ListServices"; To = "Application\Services\UseCases\ListServices" },
    @{ From = "Application\Services\UpdateService"; To = "Application\Services\UseCases\UpdateService" },
    @{ From = "Application\Users\ChangeMyPassword"; To = "Application\Users\UseCases\ChangeMyPassword" },
    @{ From = "Application\Users\CreateUser"; To = "Application\Users\UseCases\CreateUser" },
    @{ From = "Application\Users\DeleteUser"; To = "Application\Users\UseCases\DeleteUser" },
    @{ From = "Application\Users\ListUsers"; To = "Application\Users\UseCases\ListUsers" },
    @{ From = "Application\Users\UpdateMyProfile"; To = "Application\Users\UseCases\UpdateMyProfile" },
    @{ From = "Application\Vehicles\CreateVehicle"; To = "Application\Vehicles\UseCases\CreateVehicle" },
    @{ From = "Application\Vehicles\DeleteVehicle"; To = "Application\Vehicles\UseCases\DeleteVehicle" },
    @{ From = "Application\Vehicles\GetVehicleById"; To = "Application\Vehicles\UseCases\GetVehicleById" },
    @{ From = "Application\Vehicles\ListVehicles"; To = "Application\Vehicles\UseCases\ListVehicles" },
    @{ From = "Application\Vehicles\UpdateVehicle"; To = "Application\Vehicles\UseCases\UpdateVehicle" },
    @{ From = "Application\Vehicles\VehicleBrands"; To = "Application\Vehicles\UseCases\VehicleBrands" },
    @{ From = "Application\Vehicles\VehicleColors"; To = "Application\Vehicles\UseCases\VehicleColors" },
    @{ From = "Application\Vehicles\VehicleModels"; To = "Application\Vehicles\UseCases\VehicleModels" },
    @{ From = "Application\WorkOrders\AddEstimateInventoryItem"; To = "Application\WorkOrders\UseCases\AddEstimateInventoryItem" },
    @{ From = "Application\WorkOrders\AddEstimateService"; To = "Application\WorkOrders\UseCases\AddEstimateService" },
    @{ From = "Application\WorkOrders\ApproveMyEstimate"; To = "Application\WorkOrders\UseCases\ApproveMyEstimate" },
    @{ From = "Application\WorkOrders\CancelWorkOrder"; To = "Application\WorkOrders\UseCases\CancelWorkOrder" },
    @{ From = "Application\WorkOrders\CompleteEstimateService"; To = "Application\WorkOrders\UseCases\CompleteEstimateService" },
    @{ From = "Application\WorkOrders\CreateEstimate"; To = "Application\WorkOrders\UseCases\CreateEstimate" },
    @{ From = "Application\WorkOrders\CreateWorkOrder"; To = "Application\WorkOrders\UseCases\CreateWorkOrder" },
    @{ From = "Application\WorkOrders\DeliverWorkOrder"; To = "Application\WorkOrders\UseCases\DeliverWorkOrder" },
    @{ From = "Application\WorkOrders\GetAverageServiceTime"; To = "Application\WorkOrders\UseCases\GetAverageServiceTime" },
    @{ From = "Application\WorkOrders\GetMyWorkOrderById"; To = "Application\WorkOrders\UseCases\GetMyWorkOrderById" },
    @{ From = "Application\WorkOrders\GetWorkOrderById"; To = "Application\WorkOrders\UseCases\GetWorkOrderById" },
    @{ From = "Application\WorkOrders\ListMyWorkOrders"; To = "Application\WorkOrders\UseCases\ListMyWorkOrders" },
    @{ From = "Application\WorkOrders\ListWorkOrders"; To = "Application\WorkOrders\UseCases\ListWorkOrders" },
    @{ From = "Application\WorkOrders\RejectMyEstimate"; To = "Application\WorkOrders\UseCases\RejectMyEstimate" },
    @{ From = "Application\WorkOrders\StartDiagnosis"; To = "Application\WorkOrders\UseCases\StartDiagnosis" },
    @{ From = "Application\WorkOrders\StartEstimateService"; To = "Application\WorkOrders\UseCases\StartEstimateService" },
    @{ From = "Application\WorkOrders\StartWork"; To = "Application\WorkOrders\UseCases\StartWork" },
    @{ From = "Application\WorkOrders\SubmitEstimate"; To = "Application\WorkOrders\UseCases\SubmitEstimate" }
)

foreach ($move in $moves) {
    if (Test-Path -LiteralPath $move.From) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $move.To) | Out-Null
        Move-Item -LiteralPath $move.From -Destination $move.To
    }
}
```

Expected: use-case folders now live under `UseCases`.

- [ ] **Step 2: Update namespaces and usings**

Run:

```powershell
$files = Get-ChildItem -Path Application,Adapters.Api,Tests -Recurse -File -Include *.cs |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' }

$replacements = @{
    "GarageFlow.Application.Auth.Login" = "GarageFlow.Application.Auth.UseCases.Login"
    "GarageFlow.Application.Customers.ActivateCustomerPortalUser" = "GarageFlow.Application.Customers.UseCases.ActivateCustomerPortalUser"
    "GarageFlow.Application.Customers.CreateCustomer" = "GarageFlow.Application.Customers.UseCases.CreateCustomer"
    "GarageFlow.Application.Customers.DeleteCustomer" = "GarageFlow.Application.Customers.UseCases.DeleteCustomer"
    "GarageFlow.Application.Customers.GetCustomerById" = "GarageFlow.Application.Customers.UseCases.GetCustomerById"
    "GarageFlow.Application.Customers.ListCustomers" = "GarageFlow.Application.Customers.UseCases.ListCustomers"
    "GarageFlow.Application.Customers.UpdateCustomer" = "GarageFlow.Application.Customers.UseCases.UpdateCustomer"
    "GarageFlow.Application.InventoryItems.CreateInventoryItem" = "GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem"
    "GarageFlow.Application.InventoryItems.DeleteInventoryItem" = "GarageFlow.Application.InventoryItems.UseCases.DeleteInventoryItem"
    "GarageFlow.Application.InventoryItems.GetInventoryItemById" = "GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById"
    "GarageFlow.Application.InventoryItems.ListInventoryItems" = "GarageFlow.Application.InventoryItems.UseCases.ListInventoryItems"
    "GarageFlow.Application.InventoryItems.UpdateInventoryItem" = "GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItem"
    "GarageFlow.Application.InventoryItems.UpdateInventoryItemStock" = "GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItemStock"
    "GarageFlow.Application.Services.CreateService" = "GarageFlow.Application.Services.UseCases.CreateService"
    "GarageFlow.Application.Services.DeleteService" = "GarageFlow.Application.Services.UseCases.DeleteService"
    "GarageFlow.Application.Services.GetServiceById" = "GarageFlow.Application.Services.UseCases.GetServiceById"
    "GarageFlow.Application.Services.ListServices" = "GarageFlow.Application.Services.UseCases.ListServices"
    "GarageFlow.Application.Services.UpdateService" = "GarageFlow.Application.Services.UseCases.UpdateService"
    "GarageFlow.Application.Users.ChangeMyPassword" = "GarageFlow.Application.Users.UseCases.ChangeMyPassword"
    "GarageFlow.Application.Users.CreateUser" = "GarageFlow.Application.Users.UseCases.CreateUser"
    "GarageFlow.Application.Users.DeleteUser" = "GarageFlow.Application.Users.UseCases.DeleteUser"
    "GarageFlow.Application.Users.ListUsers" = "GarageFlow.Application.Users.UseCases.ListUsers"
    "GarageFlow.Application.Users.UpdateMyProfile" = "GarageFlow.Application.Users.UseCases.UpdateMyProfile"
    "GarageFlow.Application.Vehicles.CreateVehicle" = "GarageFlow.Application.Vehicles.UseCases.CreateVehicle"
    "GarageFlow.Application.Vehicles.DeleteVehicle" = "GarageFlow.Application.Vehicles.UseCases.DeleteVehicle"
    "GarageFlow.Application.Vehicles.GetVehicleById" = "GarageFlow.Application.Vehicles.UseCases.GetVehicleById"
    "GarageFlow.Application.Vehicles.ListVehicles" = "GarageFlow.Application.Vehicles.UseCases.ListVehicles"
    "GarageFlow.Application.Vehicles.UpdateVehicle" = "GarageFlow.Application.Vehicles.UseCases.UpdateVehicle"
    "GarageFlow.Application.Vehicles.VehicleBrands" = "GarageFlow.Application.Vehicles.UseCases.VehicleBrands"
    "GarageFlow.Application.Vehicles.VehicleColors" = "GarageFlow.Application.Vehicles.UseCases.VehicleColors"
    "GarageFlow.Application.Vehicles.VehicleModels" = "GarageFlow.Application.Vehicles.UseCases.VehicleModels"
    "GarageFlow.Application.WorkOrders.AddEstimateInventoryItem" = "GarageFlow.Application.WorkOrders.UseCases.AddEstimateInventoryItem"
    "GarageFlow.Application.WorkOrders.AddEstimateService" = "GarageFlow.Application.WorkOrders.UseCases.AddEstimateService"
    "GarageFlow.Application.WorkOrders.ApproveMyEstimate" = "GarageFlow.Application.WorkOrders.UseCases.ApproveMyEstimate"
    "GarageFlow.Application.WorkOrders.CancelWorkOrder" = "GarageFlow.Application.WorkOrders.UseCases.CancelWorkOrder"
    "GarageFlow.Application.WorkOrders.CompleteEstimateService" = "GarageFlow.Application.WorkOrders.UseCases.CompleteEstimateService"
    "GarageFlow.Application.WorkOrders.CreateEstimate" = "GarageFlow.Application.WorkOrders.UseCases.CreateEstimate"
    "GarageFlow.Application.WorkOrders.CreateWorkOrder" = "GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrder"
    "GarageFlow.Application.WorkOrders.DeliverWorkOrder" = "GarageFlow.Application.WorkOrders.UseCases.DeliverWorkOrder"
    "GarageFlow.Application.WorkOrders.GetAverageServiceTime" = "GarageFlow.Application.WorkOrders.UseCases.GetAverageServiceTime"
    "GarageFlow.Application.WorkOrders.GetMyWorkOrderById" = "GarageFlow.Application.WorkOrders.UseCases.GetMyWorkOrderById"
    "GarageFlow.Application.WorkOrders.GetWorkOrderById" = "GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById"
    "GarageFlow.Application.WorkOrders.ListMyWorkOrders" = "GarageFlow.Application.WorkOrders.UseCases.ListMyWorkOrders"
    "GarageFlow.Application.WorkOrders.ListWorkOrders" = "GarageFlow.Application.WorkOrders.UseCases.ListWorkOrders"
    "GarageFlow.Application.WorkOrders.RejectMyEstimate" = "GarageFlow.Application.WorkOrders.UseCases.RejectMyEstimate"
    "GarageFlow.Application.WorkOrders.StartDiagnosis" = "GarageFlow.Application.WorkOrders.UseCases.StartDiagnosis"
    "GarageFlow.Application.WorkOrders.StartEstimateService" = "GarageFlow.Application.WorkOrders.UseCases.StartEstimateService"
    "GarageFlow.Application.WorkOrders.StartWork" = "GarageFlow.Application.WorkOrders.UseCases.StartWork"
    "GarageFlow.Application.WorkOrders.SubmitEstimate" = "GarageFlow.Application.WorkOrders.UseCases.SubmitEstimate"
}

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($key in $replacements.Keys) {
        $content = $content.Replace($key, $replacements[$key])
    }
    Set-Content -LiteralPath $file.FullName -Value $content -NoNewline
}
```

Expected: no using or namespace references point to the old use-case paths.

- [ ] **Step 3: Update module convention tests for UseCases**

Edit `Tests/Unit/Architecture/ModuleConventionTests.cs`.

Replace the application file validation logic so application files are valid when they live under `UseCases`, `Ports`, `ReadModels`, `Abstractions`, or `Common`.

Use this helper:

```csharp
private static bool IsValidApplicationFile(string filePath)
{
    var relativePath = ToRelativePath(filePath);
    var pathSegments = relativePath.Split('/');

    if (pathSegments.Length < 3)
    {
        return false;
    }

    if (!string.Equals(pathSegments[0], "Application", StringComparison.Ordinal))
    {
        return false;
    }

    if (pathSegments.Contains("UseCases") &&
        HasAnySuffix(filePath, "Command.cs", "Query.cs", "Handler.cs", "Result.cs", "Dto.cs"))
    {
        return true;
    }

    if (pathSegments.Contains("Ports") && Path.GetFileName(filePath).StartsWith('I'))
    {
        return true;
    }

    if (pathSegments.Contains("ReadModels") && HasAnySuffix(filePath, "ReadModel.cs", "Projection.cs", "Dto.cs"))
    {
        return true;
    }

    if (pathSegments.Contains("Abstractions") && Path.GetFileName(filePath).StartsWith('I'))
    {
        return true;
    }

    return pathSegments.Contains("Common");
}
```

- [ ] **Step 4: Validate use-case move**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture"
```

Expected: both commands PASS.

- [ ] **Step 5: Commit UseCases structure**

Run:

```powershell
git add Application Adapters.Api Tests
git commit -m "refactor: move application use cases under use cases"
```

Expected: commit succeeds.

## Task 6: Move Domain Repository Ports And Read Models To Application

**Files:**
- Move: `Domain/*/Repositories/I*Repository.cs` to `Application/*/Ports/I*Repository.cs`
- Move: `Domain/*/Repositories/*ReadModel.cs` to `Application/*/ReadModels/*ReadModel.cs`
- Modify: `Application`, `Adapters.Infrastructure`, and `Tests` usings
- Modify: `Tests/Unit/Architecture/ModuleConventionTests.cs`

- [ ] **Step 1: Move repository contracts and read models**

Run:

```powershell
$modules = @("Customers", "InventoryItems", "Services", "Users", "Vehicles", "WorkOrders")

foreach ($module in $modules) {
    $source = "Domain\$module\Repositories"
    if (-not (Test-Path -LiteralPath $source)) {
        continue
    }

    $ports = "Application\$module\Ports"
    $readModels = "Application\$module\ReadModels"
    New-Item -ItemType Directory -Force -Path $ports,$readModels | Out-Null

    Get-ChildItem -LiteralPath $source -File -Filter "I*Repository.cs" |
        ForEach-Object { Move-Item -LiteralPath $_.FullName -Destination $ports }

    Get-ChildItem -LiteralPath $source -File -Filter "*ReadModel.cs" |
        ForEach-Object { Move-Item -LiteralPath $_.FullName -Destination $readModels }

    if ((Get-ChildItem -LiteralPath $source -File -ErrorAction SilentlyContinue).Count -eq 0) {
        Remove-Item -LiteralPath $source -Force
    }
}
```

Expected: no `Domain\<Module>\Repositories` files remain.

- [ ] **Step 2: Update namespaces for moved files and consumers**

Run:

```powershell
$files = Get-ChildItem -Path Application,Adapters.Infrastructure,Tests -Recurse -File -Include *.cs |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' }

$moduleNames = @("Customers", "InventoryItems", "Services", "Users", "Vehicles", "WorkOrders")

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($module in $moduleNames) {
        $content = $content.Replace("GarageFlow.Domain.$module.Repositories", "GarageFlow.Application.$module.Ports")
    }
    Set-Content -LiteralPath $file.FullName -Value $content -NoNewline
}

$readModelFiles = Get-ChildItem -Path Application -Recurse -File -Filter "*ReadModel.cs" |
    Where-Object { $_.FullName -match '\\ReadModels\\' }

foreach ($file in $readModelFiles) {
    $relative = Resolve-Path -LiteralPath $file.FullName -Relative
    $parts = $relative -split '\\'
    $module = $parts[2]
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $content = $content -replace "namespace GarageFlow\.Application\.$module\.Ports;", "namespace GarageFlow.Application.$module.ReadModels;"
    Set-Content -LiteralPath $file.FullName -Value $content -NoNewline
}
```

Expected: repository interfaces use `Application.<Module>.Ports`; read models use `Application.<Module>.ReadModels`.

- [ ] **Step 3: Add read model usings where ports reference them**

For each moved `I*Repository.cs` file that returns read models, add the matching read model namespace.

Example for `Application/WorkOrders/Ports/IWorkOrderRepository.cs`:

```csharp
using GarageFlow.Application.WorkOrders.ReadModels;
```

Example for `Application/Vehicles/Ports/IVehicleRepository.cs`:

```csharp
using GarageFlow.Application.Vehicles.ReadModels;
```

Example for `Application/InventoryItems/Ports/IInventoryItemRepository.cs`:

```csharp
using GarageFlow.Application.InventoryItems.ReadModels;
```

- [ ] **Step 4: Replace Domain repository convention test**

In `Tests/Unit/Architecture/ModuleConventionTests.cs`, replace the old `Domain_RepositoryContracts_ShouldFollowInterfaceNaming` test with:

```csharp
[Fact]
public void Domain_ShouldNotContainRepositoryOrReadModelFolders()
{
    var invalidPaths = new List<string>();

    foreach (var module in BusinessModules)
    {
        var repositoryPath = Path.Combine(RepositoryRoot, "Domain", module, "Repositories");
        if (Directory.Exists(repositoryPath))
        {
            invalidPaths.Add(ToRelativePath(repositoryPath));
        }
    }

    Assert.True(
        invalidPaths.Count == 0,
        $"Domain must not contain repository/read-model folders: {string.Join(", ", invalidPaths)}");
}
```

- [ ] **Step 5: Validate port move**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~GarageFlow.Tests.Unit.Architecture"
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~Handlers"
```

Expected: all commands PASS.

- [ ] **Step 6: Commit ports/read models move**

Run:

```powershell
git add Application Domain Adapters.Infrastructure Tests
git add -u Domain
git commit -m "refactor: move ports and read models to application"
```

Expected: commit succeeds.

## Task 7: Split Inventory And Vehicle Query Ports From Mutation Repositories

**Files:**
- Create: `Application/InventoryItems/Ports/IInventoryItemQueries.cs`
- Create: `Application/Vehicles/Ports/IVehicleQueries.cs`
- Modify: `Application/InventoryItems/Ports/IInventoryItemRepository.cs`
- Modify: `Application/Vehicles/Ports/IVehicleRepository.cs`
- Modify: `Adapters.Infrastructure/InventoryItems/Repositories/InventoryItemRepository.cs`
- Modify: `Adapters.Infrastructure/Vehicles/Repositories/VehicleRepository.cs`
- Create: `Adapters.Infrastructure/InventoryItems/Repositories/EfInventoryItemQueries.cs`
- Create: `Adapters.Infrastructure/Vehicles/Repositories/EfVehicleQueries.cs`
- Modify: inventory and vehicle query handlers
- Modify: DI registration

- [ ] **Step 1: Add inventory query port**

Create `Application/InventoryItems/Ports/IInventoryItemQueries.cs`:

```csharp
using GarageFlow.Application.InventoryItems.ReadModels;

namespace GarageFlow.Application.InventoryItems.Ports;

public interface IInventoryItemQueries
{
    Task<(IReadOnlyList<InventoryItemDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Add vehicle query port**

Create `Application/Vehicles/Ports/IVehicleQueries.cs`:

```csharp
using GarageFlow.Application.Vehicles.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Application.Vehicles.Ports;

public interface IVehicleQueries
{
    Task<VehicleDetailsReadModel?> GetDetailsByIdAsync(
        VehicleId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDetailsReadModel>> ListDetailsByCustomerIdAsync(
        CustomerId customerId,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Trim repository ports**

Remove `ListDetailsAsync` from `Application/InventoryItems/Ports/IInventoryItemRepository.cs`.

Remove these members from `Application/Vehicles/Ports/IVehicleRepository.cs`:

```csharp
Task<VehicleDetailsReadModel?> GetDetailsByIdAsync(
    VehicleId id,
    CancellationToken cancellationToken = default);

Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
    int page,
    int pageSize,
    CustomerId? customerId = null,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<VehicleDetailsReadModel>> ListDetailsByCustomerIdAsync(
    CustomerId customerId,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Create EF inventory queries**

Create `Adapters.Infrastructure/InventoryItems/Repositories/EfInventoryItemQueries.cs` by moving the current `ListDetailsAsync` implementation from `InventoryItemRepository` into this class:

```csharp
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.InventoryItems.ReadModels;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.InventoryItems.Repositories;

public sealed class EfInventoryItemQueries(GarageFlowDbContext dbContext) : IInventoryItemQueries
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<(IReadOnlyList<InventoryItemDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InventoryItems.AsNoTracking()
            .OrderBy(inventoryItem => inventoryItem.CreatedAt)
            .ThenBy(inventoryItem => inventoryItem.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var readModels = items.Select(inventoryItem => new InventoryItemDetailsReadModel(
            inventoryItem.Id.Value,
            inventoryItem.Name.Value,
            inventoryItem.Description.Value,
            inventoryItem.Type,
            inventoryItem.Cost.Value,
            inventoryItem.Price.Value,
            inventoryItem.StockQuantity.Value,
            inventoryItem.CreatedAt))
            .ToList();

        return (readModels, totalCount);
    }
}
```

- [ ] **Step 5: Create EF vehicle queries**

Create `Adapters.Infrastructure/Vehicles/Repositories/EfVehicleQueries.cs` by moving detail query logic from `VehicleRepository` into this class:

```csharp
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Application.Vehicles.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.Vehicles.Repositories;

public sealed class EfVehicleQueries(GarageFlowDbContext dbContext) : IVehicleQueries
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<VehicleDetailsReadModel?> GetDetailsByIdAsync(VehicleId id, CancellationToken cancellationToken = default)
    {
        return await CreateVehicleDetailsQuery(vehicleId: id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = CreateVehicleDetailsQuery(customerId: customerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<VehicleDetailsReadModel>> ListDetailsByCustomerIdAsync(
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        return await CreateVehicleDetailsQuery(customerId: customerId).ToListAsync(cancellationToken);
    }

    private IQueryable<VehicleDetailsReadModel> CreateVehicleDetailsQuery(
        VehicleId? vehicleId = null,
        CustomerId? customerId = null)
    {
        var vehicles = _dbContext.Vehicles.AsNoTracking();

        if (vehicleId is not null)
        {
            vehicles = vehicles.Where(vehicle => vehicle.Id == vehicleId);
        }

        if (customerId is not null)
        {
            vehicles = vehicles.Where(vehicle => vehicle.CustomerId == customerId);
        }

        return from vehicle in vehicles
               join brand in _dbContext.VehicleBrands.AsNoTracking() on vehicle.VehicleBrandId equals brand.Id
               join model in _dbContext.VehicleModels.AsNoTracking()
                   on new { vehicle.VehicleModelId, vehicle.VehicleBrandId }
                   equals new { VehicleModelId = model.Id, model.VehicleBrandId }
               join color in _dbContext.VehicleColors.AsNoTracking() on vehicle.VehicleColorId equals color.Id
               orderby vehicle.CreatedAt, vehicle.Id
               select new VehicleDetailsReadModel(
                   vehicle.Id.Value,
                   vehicle.CustomerId.Value,
                   vehicle.Year.Value,
                   vehicle.VehicleBrandId.Value,
                   EF.Property<string>(brand, nameof(VehicleBrand.Name)),
                   vehicle.VehicleModelId.Value,
                   EF.Property<string>(model, nameof(VehicleModel.Name)),
                   vehicle.VehicleColorId.Value,
                   EF.Property<string>(color, nameof(VehicleColor.Name)),
                   vehicle.LicensePlate.Value,
                   vehicle.CreatedAt);
    }
}
```

- [ ] **Step 6: Update handlers to use query ports**

Change these handlers to inject query ports:

```text
Application/InventoryItems/UseCases/ListInventoryItems/ListInventoryItemsHandler.cs -> IInventoryItemQueries
Application/Vehicles/UseCases/GetVehicleById/GetVehicleByIdHandler.cs -> IVehicleQueries
Application/Vehicles/UseCases/ListVehicles/ListVehiclesHandler.cs -> IVehicleQueries
Application/Customers/UseCases/GetCustomerById/GetCustomerByIdHandler.cs -> IVehicleQueries
```

In each handler, replace repository detail/list calls with the query port method of the same name.

- [ ] **Step 7: Register query implementations**

In `Adapters.Infrastructure/DataAccess/DependencyInjection.cs`, add:

```csharp
services.AddScoped<IInventoryItemQueries, EfInventoryItemQueries>();
services.AddScoped<IVehicleQueries, EfVehicleQueries>();
```

Keep existing repository registrations for mutation commands.

- [ ] **Step 8: Validate inventory and vehicle query split**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~InventoryItems|FullyQualifiedName~Vehicles|FullyQualifiedName~Customers"
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~InventoryItems|FullyQualifiedName~Vehicles|FullyQualifiedName~Customers"
```

Expected: all commands PASS.

- [ ] **Step 9: Commit inventory and vehicle query ports**

Run:

```powershell
git add Application Adapters.Infrastructure Tests
git commit -m "refactor: split inventory and vehicle query ports"
```

Expected: commit succeeds.

## Task 8: Split WorkOrder Query Port From Mutation Repository

**Files:**
- Create: `Application/WorkOrders/Ports/IWorkOrderQueries.cs`
- Modify: `Application/WorkOrders/Ports/IWorkOrderRepository.cs`
- Modify: `Adapters.Infrastructure/WorkOrders/Repositories/WorkOrderRepository.cs`
- Create: `Adapters.Infrastructure/WorkOrders/Repositories/EfWorkOrderQueries.cs`
- Modify: all WorkOrders query handlers
- Modify: DI registration

- [ ] **Step 1: Add work-order query port**

Create `Application/WorkOrders/Ports/IWorkOrderQueries.cs`:

```csharp
using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Application.WorkOrders.Ports;

public interface IWorkOrderQueries
{
    Task<WorkOrderDetailsReadModel?> GetDetailsByIdAsync(
        WorkOrderId id,
        CancellationToken cancellationToken = default);

    Task<WorkOrderDetailsReadModel?> GetCustomerDetailsByIdAsync(
        WorkOrderId id,
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListCustomerDetailsAsync(
        int page,
        int pageSize,
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
        DateTime completedFrom,
        DateTime completedTo,
        ServiceId? serviceId = null,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Trim work-order mutation repository**

Edit `Application/WorkOrders/Ports/IWorkOrderRepository.cs` so it contains only:

```csharp
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Application.WorkOrders.Ports;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(WorkOrderId id, CancellationToken cancellationToken = default);

    Task<WorkOrder?> GetByIdForEstimateMutationAsync(WorkOrderId id, CancellationToken cancellationToken = default);

    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Move query implementation into EfWorkOrderQueries**

Create `Adapters.Infrastructure/WorkOrders/Repositories/EfWorkOrderQueries.cs`.

Move these methods and their helper methods from `WorkOrderRepository` into `EfWorkOrderQueries`:

```text
GetDetailsByIdAsync
GetCustomerDetailsByIdAsync
ListDetailsAsync
ListCustomerDetailsAsync
GetAverageServiceTimeAsync
CreateWorkOrderDetailsQuery
MapToReadModel
```

The new class header should be:

```csharp
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;

public sealed class EfWorkOrderQueries(GarageFlowDbContext dbContext) : IWorkOrderQueries
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
}
```

After moving query code, `WorkOrderRepository` should only contain aggregate loading, estimate mutation locking, and `AddAsync`.

- [ ] **Step 4: Update query handlers to use IWorkOrderQueries**

Change these handlers to inject `IWorkOrderQueries`:

```text
Application/WorkOrders/UseCases/GetAverageServiceTime/GetAverageServiceTimeHandler.cs
Application/WorkOrders/UseCases/GetMyWorkOrderById/GetMyWorkOrderByIdHandler.cs
Application/WorkOrders/UseCases/GetWorkOrderById/GetWorkOrderByIdHandler.cs
Application/WorkOrders/UseCases/ListMyWorkOrders/ListMyWorkOrdersHandler.cs
Application/WorkOrders/UseCases/ListWorkOrders/ListWorkOrdersHandler.cs
```

Replace detail/list/average calls on `IWorkOrderRepository` with calls on `IWorkOrderQueries`.

- [ ] **Step 5: Register work-order query implementation**

In `Adapters.Infrastructure/DataAccess/DependencyInjection.cs`, add:

```csharp
services.AddScoped<IWorkOrderQueries, EfWorkOrderQueries>();
```

- [ ] **Step 6: Validate work-order query split**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrders"
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~WorkOrders"
```

Expected: all commands PASS.

- [ ] **Step 7: Commit work-order query port**

Run:

```powershell
git add Application Adapters.Infrastructure Tests
git commit -m "refactor: split work order query port"
```

Expected: commit succeeds.

## Task 9: Harden API And Application Boundary Contracts For Domain Enums

**Files:**
- Modify: inventory item API request/response contracts
- Modify: inventory item Application commands/results/read models
- Modify: user API request/response contracts
- Modify: user Application commands/results/read models
- Modify: related handlers and tests

- [ ] **Step 1: Preserve public JSON enum shape with integer contracts**

Use `int` for public HTTP and application boundary fields that currently serialize as numeric enums:

```text
InventoryItem Type -> int Type
User Role -> int Role
```

The domain handlers must parse those integers into domain enums internally.

- [ ] **Step 2: Update inventory Application boundary types**

Change these Application types so `Type` is `int`:

```text
Application/InventoryItems/UseCases/CreateInventoryItem/CreateInventoryItemCommand.cs
Application/InventoryItems/UseCases/CreateInventoryItem/CreateInventoryItemResult.cs
Application/InventoryItems/UseCases/UpdateInventoryItem/UpdateInventoryItemCommand.cs
Application/InventoryItems/UseCases/UpdateInventoryItem/UpdateInventoryItemResult.cs
Application/InventoryItems/UseCases/UpdateInventoryItemStock/UpdateInventoryItemStockResult.cs
Application/InventoryItems/UseCases/GetInventoryItemById/InventoryItemDto.cs
Application/InventoryItems/ReadModels/InventoryItemDetailsReadModel.cs
```

In inventory handlers, convert the integer to the domain enum with:

```csharp
if (!Enum.IsDefined(typeof(InventoryItemType), request.Type))
{
    throw new ValidationException($"Inventory item type '{request.Type}' is invalid.");
}

var inventoryItemType = (InventoryItemType)request.Type;
```

Return `(int)inventoryItem.Type` in Application results and DTOs.

- [ ] **Step 3: Update inventory API contracts**

Change these API contracts so `Type` is `int` and remove `using GarageFlow.Domain.InventoryItems.Enums;`:

```text
Adapters.Api/InventoryItems/CreateInventoryItem/CreateInventoryItemRequest.cs
Adapters.Api/InventoryItems/CreateInventoryItem/CreateInventoryItemResponse.cs
Adapters.Api/InventoryItems/UpdateInventoryItem/UpdateInventoryItemRequest.cs
Adapters.Api/InventoryItems/GetInventoryItemById/InventoryItemResponse.cs
Adapters.Api/InventoryItems/ListInventoryItems/ListInventoryItemsResponse.cs
```

Keep endpoint mapping by passing `request.Type` into commands and `result.Type` into responses.

- [ ] **Step 4: Update user Application boundary types**

Change these Application types so `Role` is `int`:

```text
Application/Users/UseCases/CreateUser/CreateUserCommand.cs
Application/Users/UseCases/CreateUser/CreateUserResult.cs
Application/Users/UseCases/ListUsers/UserListItem.cs
Application/Users/UseCases/UpdateMyProfile/UpdateMyProfileResult.cs
```

In `CreateUserHandler`, convert the integer to the domain enum with:

```csharp
if (!Enum.IsDefined(typeof(UserRole), request.Role))
{
    throw new ValidationException($"User role '{request.Role}' is invalid.");
}

var role = (UserRole)request.Role;
```

Use `role` for domain decisions and return `(int)user.Role` in results.

- [ ] **Step 5: Update user API contracts**

Change these API contracts so `Role` is `int` and remove `using GarageFlow.Domain.Users.Enums;`:

```text
Adapters.Api/Users/CreateUser/CreateUserRequest.cs
Adapters.Api/Users/CreateUser/CreateUserResponse.cs
Adapters.Api/Users/ListUsers/UserListItemResponse.cs
Adapters.Api/Users/UpdateMyProfile/UpdateMyProfileResponse.cs
```

Keep customer portal activation responses that already expose `Role` as `string`.

- [ ] **Step 6: Update integration test contracts**

Change these test contracts to avoid domain enum references:

```text
Tests/Integration/Api/InventoryItems/Contracts/InventoryItemResponse.cs
Tests/Integration/Api/Users/Contracts/CreateUserResponse.cs
Tests/Integration/Api/Users/Contracts/UpdateMyProfileResponse.cs
Tests/Integration/Api/Users/Contracts/UserListItemResponse.cs
```

Use:

```csharp
int Type
```

for inventory item contracts and:

```csharp
int Role
```

for user contracts.

Update assertions that compare domain enums to compare integer values:

```csharp
Assert.Equal((int)UserRole.Attendant, createdUser.Role);
```

- [ ] **Step 7: Add architecture test for entire API adapter**

In `Tests/Unit/Architecture/DependencyRulesTests.cs`, add:

```csharp
[Fact]
public void ApiAdapter_ShouldNotDependOnDomainInfrastructureHostOrSharedKernel()
{
    var result = Types
        .InAssembly(LoadAssembly("GarageFlow.Adapters.Api", "Adapters.Api"))
        .That()
        .ResideInNamespaceStartingWith(ApiNamespace)
        .ShouldNot()
        .HaveDependencyOnAny(DomainNamespace, InfrastructureNamespace, SharedKernelNamespace, HostNamespace)
        .GetResult();

    AssertRule(result, nameof(ApiAdapter_ShouldNotDependOnDomainInfrastructureHostOrSharedKernel));
}
```

Add:

```csharp
private const string HostNamespace = "GarageFlow.Host";
```

- [ ] **Step 8: Validate API boundary hardening**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~Architecture|FullyQualifiedName~InventoryItems|FullyQualifiedName~Users"
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~InventoryItems|FullyQualifiedName~Users"
dotnet test Tests\E2E\GarageFlow.Tests.E2E.csproj --filter "FullyQualifiedName~InventoryItems|FullyQualifiedName~Users"
```

Expected: all commands PASS. If Docker is unavailable, record that E2E could not run and run it before final migration completion.

- [ ] **Step 9: Commit API boundary hardening**

Run:

```powershell
git add Application Adapters.Api Tests
git commit -m "refactor: remove domain enums from api boundary"
```

Expected: commit succeeds.

## Task 10: Add Transaction Pipeline And Domain Event Dispatch

**Files:**
- Create: `Application/Common/Messaging/ICommand.cs`
- Create: `Application/Common/Behaviors/TransactionBehavior.cs`
- Create: `Application/Common/Events/DomainEventNotification.cs`
- Create: `Application/Common/Events/IDomainEventDispatcher.cs`
- Create: `Application/Common/Events/MediatorDomainEventDispatcher.cs`
- Create: `SharedKernel/Domain/Events/IHasDomainEvents.cs`
- Modify: `SharedKernel/Domain/Entities/Entity.cs`
- Modify: `SharedKernel/Persistence/IUnitOfWork.cs`
- Modify: `Adapters.Infrastructure/DataAccess/GarageFlowDbContext.cs`
- Modify: command records
- Modify: handlers to remove manual transaction boilerplate
- Modify: `Host/Program.cs`

- [ ] **Step 1: Add command marker interfaces**

Create `Application/Common/Messaging/ICommand.cs`:

```csharp
using Mediator;

namespace GarageFlow.Application.Common.Messaging;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface ICommand : ICommand<Unit>;
```

- [ ] **Step 2: Convert command records to ICommand**

For every `Application/**/UseCases/**/*Command.cs`, replace:

```csharp
: IRequest<
```

with:

```csharp
: ICommand<
```

For command records returning `Unit`, replace:

```csharp
: IRequest<Unit>
```

with:

```csharp
: ICommand
```

Add:

```csharp
using GarageFlow.Application.Common.Messaging;
```

to command files, and remove `using Mediator;` from command files that only needed `IRequest`.

- [ ] **Step 3: Add domain event holder interface**

Create `SharedKernel/Domain/Events/IHasDomainEvents.cs`:

```csharp
namespace GarageFlow.SharedKernel.Domain.Events;

public interface IHasDomainEvents
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
```

Modify `SharedKernel/Domain/Entities/Entity.cs` so the class declaration is:

```csharp
public abstract class Entity<TId> : IHasDomainEvents where TId : struct
```

- [ ] **Step 4: Extend unit of work with domain event dequeue**

Modify `SharedKernel/Persistence/IUnitOfWork.cs`:

```csharp
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.SharedKernel.Persistence;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<DomainEvent> DequeueDomainEvents();
}
```

- [ ] **Step 5: Implement domain event dequeue in DbContext**

Add this method to `Adapters.Infrastructure/DataAccess/GarageFlowDbContext.cs`:

```csharp
public IReadOnlyList<DomainEvent> DequeueDomainEvents()
{
    var entities = ChangeTracker
        .Entries()
        .Select(entry => entry.Entity)
        .OfType<IHasDomainEvents>()
        .Where(entity => entity.DomainEvents.Count > 0)
        .ToList();

    var domainEvents = entities
        .SelectMany(entity => entity.DomainEvents)
        .ToList();

    foreach (var entity in entities)
    {
        entity.ClearDomainEvents();
    }

    return domainEvents;
}
```

Add these usings:

```csharp
using GarageFlow.SharedKernel.Domain.Events;
```

- [ ] **Step 6: Add domain event notification wrapper and dispatcher**

Create `Application/Common/Events/DomainEventNotification.cs`:

```csharp
using GarageFlow.SharedKernel.Domain.Events;
using Mediator;

namespace GarageFlow.Application.Common.Events;

public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : DomainEvent;
```

Create `Application/Common/Events/IDomainEventDispatcher.cs`:

```csharp
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Application.Common.Events;

public interface IDomainEventDispatcher
{
    ValueTask DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken);
}
```

Create `Application/Common/Events/MediatorDomainEventDispatcher.cs`:

```csharp
using GarageFlow.SharedKernel.Domain.Events;
using Mediator;

namespace GarageFlow.Application.Common.Events;

public sealed class MediatorDomainEventDispatcher(IMediator mediator) : IDomainEventDispatcher
{
    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    public async ValueTask DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent)
                ?? throw new InvalidOperationException($"Could not create notification for domain event '{domainEvent.GetType().Name}'.");

            await _mediator.Publish((INotification)notification, cancellationToken);
        }
    }
}
```

- [ ] **Step 7: Add transaction behavior**

Create `Application/Common/Behaviors/TransactionBehavior.cs`:

```csharp
using GarageFlow.Application.Common.Events;
using GarageFlow.Application.Common.Messaging;
using GarageFlow.SharedKernel.Persistence;
using Mediator;

namespace GarageFlow.Application.Common.Behaviors;

public sealed class TransactionBehavior<TMessage, TResponse>(
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : ICommand<TResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IDomainEventDispatcher _domainEventDispatcher =
        domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));

    public async ValueTask<TResponse> Handle(
        TMessage message,
        CancellationToken cancellationToken,
        MessageHandlerDelegate<TMessage, TResponse> next)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(message, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var domainEvents = _unitOfWork.DequeueDomainEvents();
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

            return response;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
```

- [ ] **Step 8: Register dispatcher and pipeline**

In `Host/Program.cs`, update mediator registration:

```csharp
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(TransactionBehavior<,>)];
});
```

Add:

```csharp
using GarageFlow.Application.Common.Behaviors;
using GarageFlow.Application.Common.Events;
```

Register the dispatcher:

```csharp
builder.Services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();
```

- [ ] **Step 9: Remove manual transactions from mutating handlers**

For every command handler that injects `IUnitOfWork` only for `BeginTransactionAsync`, `CommitTransactionAsync`, and `RollbackTransactionAsync`, remove the `IUnitOfWork` constructor parameter and private field.

Replace this pattern:

```csharp
await _unitOfWork.BeginTransactionAsync(cancellationToken);

try
{
    // handler logic
    await _unitOfWork.CommitTransactionAsync(cancellationToken);
    return result;
}
catch
{
    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
    throw;
}
```

with only the handler logic and `return result;`.

Keep direct `IUnitOfWork` usage only when a handler has a documented reason to control transaction timing itself.

- [ ] **Step 10: Validate transaction behavior**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj
```

Expected: all commands PASS.

- [ ] **Step 11: Commit transaction behavior**

Run:

```powershell
git add Application SharedKernel Adapters.Infrastructure Host Tests
git commit -m "refactor: centralize command transactions"
```

Expected: commit succeeds.

## Task 11: Move Estimate Approval Email To Domain Event Handler

**Files:**
- Create: `Domain/WorkOrders/Events/EstimateWaitingApprovalRequested.cs`
- Modify: `Domain/WorkOrders/Entities/WorkOrder.cs`
- Modify: `Application/WorkOrders/UseCases/SubmitEstimate/SubmitEstimateHandler.cs`
- Create: `Application/WorkOrders/Events/SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler.cs`
- Modify: `Tests/Unit/WorkOrders/WorkOrderTests.cs`
- Modify: `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`

- [ ] **Step 1: Add explicit domain event**

Create `Domain/WorkOrders/Events/EstimateWaitingApprovalRequested.cs`:

```csharp
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record EstimateWaitingApprovalRequested(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    CustomerId CustomerId,
    DateTime RequestedAt) : DomainEvent;
```

- [ ] **Step 2: Raise event from WorkOrder**

In `Domain/WorkOrders/Entities/WorkOrder.cs`, update `SubmitEstimate`.

After `AdvanceStatusForEstimateSubmission();` and `estimate.Submit();`, add:

```csharp
if (Status == WorkOrderStatus.WaitingApproval)
{
    RaiseDomainEvent(new EstimateWaitingApprovalRequested(
        WorkOrderId: Id,
        EstimateId: estimate.Id,
        CustomerId: CustomerId,
        RequestedAt: DateTime.UtcNow));
}
```

Keep the existing `EstimateSubmitted` event.

- [ ] **Step 3: Simplify SubmitEstimateHandler**

Edit `Application/WorkOrders/UseCases/SubmitEstimate/SubmitEstimateHandler.cs`.

Remove dependencies on:

```csharp
ICustomerApprovalEmailSender
ILogger<SubmitEstimateHandler>
NullLogger<SubmitEstimateHandler>
WorkOrderStatus
```

The handler constructor should only inject:

```csharp
IWorkOrderRepository workOrderRepository
```

The handler body should:

```csharp
var workOrderId = WorkOrderId.From(request.WorkOrderId);
var estimateId = EstimateId.From(request.EstimateId);

var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
if (workOrder is null)
{
    throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
}

workOrder.SubmitEstimate(estimateId);
return Unit.Value;
```

- [ ] **Step 4: Add event notification handler**

Create `Application/WorkOrders/Events/SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler.cs`:

```csharp
using GarageFlow.Application.Common.Events;
using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.Domain.WorkOrders.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Application.WorkOrders.Events;

public sealed partial class SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler(
    ICustomerApprovalEmailSender emailSender,
    ILogger<SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler> logger)
    : INotificationHandler<DomainEventNotification<EstimateWaitingApprovalRequested>>
{
    private readonly ICustomerApprovalEmailSender _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    private readonly ILogger<SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask Handle(
        DomainEventNotification<EstimateWaitingApprovalRequested> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        try
        {
            await _emailSender.SendEstimateWaitingApprovalAsync(
                domainEvent.WorkOrderId.Value,
                domainEvent.EstimateId.Value,
                domainEvent.CustomerId.Value,
                cancellationToken);
        }
        catch (Exception exception)
        {
            LogApprovalEmailFailure(
                _logger,
                domainEvent.WorkOrderId.Value,
                domainEvent.EstimateId.Value,
                exception);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to send approval email after commit for work order {WorkOrderId} and estimate {EstimateId}.")]
    private static partial void LogApprovalEmailFailure(
        ILogger logger,
        Guid workOrderId,
        Guid estimateId,
        Exception exception);
}
```

- [ ] **Step 5: Add domain test for event**

In `Tests/Unit/WorkOrders/WorkOrderTests.cs`, add a test that submits a draft estimate and asserts:

```csharp
var domainEvent = Assert.Single(workOrder.DomainEvents.OfType<EstimateWaitingApprovalRequested>());
Assert.Equal(workOrder.Id, domainEvent.WorkOrderId);
Assert.Equal(estimate.Id, domainEvent.EstimateId);
Assert.Equal(workOrder.CustomerId, domainEvent.CustomerId);
```

- [ ] **Step 6: Update handler tests**

In `Tests/Unit/WorkOrders/WorkOrderHandlersTests.cs`, remove expectations that `SubmitEstimateHandler` directly calls `ICustomerApprovalEmailSender`.

Add a focused test for `SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler`:

```csharp
[Fact]
public async Task WaitingApprovalHandler_ShouldSendApprovalEmail()
{
    var emailSender = new Mock<ICustomerApprovalEmailSender>();
    var logger = Mock.Of<ILogger<SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler>>();
    var handler = new SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler(emailSender.Object, logger);
    var domainEvent = new EstimateWaitingApprovalRequested(
        WorkOrderId.From(Guid.NewGuid()),
        EstimateId.From(Guid.NewGuid()),
        CustomerId.From(Guid.NewGuid()),
        DateTime.UtcNow);

    await handler.Handle(new DomainEventNotification<EstimateWaitingApprovalRequested>(domainEvent), CancellationToken.None);

    emailSender.Verify(
        sender => sender.SendEstimateWaitingApprovalAsync(
            domainEvent.WorkOrderId.Value,
            domainEvent.EstimateId.Value,
            domainEvent.CustomerId.Value,
            It.IsAny<CancellationToken>()),
        Times.Once);
}
```

- [ ] **Step 7: Validate event side effect**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj --filter "FullyQualifiedName~WorkOrders"
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj --filter "FullyQualifiedName~WorkOrders"
```

Expected: all commands PASS.

- [ ] **Step 8: Commit event-handler email flow**

Run:

```powershell
git add Domain Application Tests
git commit -m "refactor: send approval email from domain event"
```

Expected: commit succeeds.

## Task 12: Final Architecture Rules And Documentation

**Files:**
- Modify: `Tests/Unit/Architecture/DependencyRulesTests.cs`
- Modify: `Tests/Unit/Architecture/ModuleConventionTests.cs`
- Modify: `AGENTS.md`
- Modify: `README.md`
- Verify: `docs/superpowers/specs/2026-07-04-clean-architecture-ddd-modular-migration-design.md`

- [ ] **Step 1: Add final dependency constants**

In `Tests/Unit/Architecture/DependencyRulesTests.cs`, ensure constants are:

```csharp
private const string ApiNamespace = "GarageFlow.Adapters.Api";
private const string ApplicationNamespace = "GarageFlow.Application";
private const string DomainNamespace = "GarageFlow.Domain";
private const string HostNamespace = "GarageFlow.Host";
private const string InfrastructureNamespace = "GarageFlow.Adapters.Infrastructure";
private const string SharedKernelNamespace = "GarageFlow.SharedKernel";
```

- [ ] **Step 2: Add final forbidden dependency tests**

Add tests that enforce:

```text
SharedKernel -> no GarageFlow production project
Domain -> only SharedKernel
Application -> Domain and SharedKernel only
Adapters.Api -> Application only
Adapters.Infrastructure -> Application, Domain, SharedKernel only
Host -> may depend on Adapters.Api, Adapters.Infrastructure, Application, Domain, SharedKernel
```

Use `Types.InAssembly(...).ShouldNot().HaveDependencyOnAny(...)` for each forbidden group.

- [ ] **Step 3: Add Domain purity file-system test**

In `ModuleConventionTests.cs`, add a test that fails if any file under `Domain` contains forbidden tokens:

```csharp
private static readonly string[] ForbiddenDomainTokens =
[
    "Mediator",
    "Microsoft.EntityFrameworkCore",
    "Microsoft.AspNetCore",
    "ReadModel",
    "Dto",
    "Repository"
];
```

The test should scan `Domain/**/*.cs`, read file text, and assert no forbidden token exists.

- [ ] **Step 4: Update AGENTS architecture instructions**

Update `AGENTS.md` layer list to:

```text
- GarageFlow.SharedKernel
- GarageFlow.Domain
- GarageFlow.Application
- GarageFlow.Adapters.Api
- GarageFlow.Adapters.Infrastructure
- GarageFlow.Host
```

Update dependency direction to match:

```text
Host -> Adapters.Api, Adapters.Infrastructure, Application
Adapters.Api -> Application
Adapters.Infrastructure -> Application, Domain, SharedKernel
Application -> Domain, SharedKernel
Domain -> SharedKernel
SharedKernel -> no GarageFlow production project
```

Update canonical module layout to use `Adapters.Api`, `Application/<Module>/UseCases`, `Application/<Module>/Ports`, `Application/<Module>/ReadModels`, and `Adapters.Infrastructure`.

- [ ] **Step 5: Update README architecture section**

In `README.md`, replace old layer references with the new layer names and include this dependency diagram:

```text
Host
  -> Adapters.Api -> Application -> Domain -> SharedKernel
  -> Adapters.Infrastructure -> Application / Domain / SharedKernel
```

- [ ] **Step 6: Run full validation**

Run:

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests\Unit\GarageFlow.Tests.Unit.csproj
dotnet test Tests\Integration\GarageFlow.Tests.Integration.csproj
dotnet test Tests\E2E\GarageFlow.Tests.E2E.csproj
dotnet test GarageFlow.slnx
```

Expected: all commands PASS. If E2E cannot run because Docker is unavailable, do not mark the migration complete until E2E is run in an environment with Docker.

- [ ] **Step 7: Commit final architecture rules and docs**

Run:

```powershell
git add Tests AGENTS.md README.md
git commit -m "docs: align architecture rules with clean adapters"
```

Expected: commit succeeds.

## Final Completion Checklist

- [ ] `GarageFlow.Host` is the only executable project.
- [ ] `GarageFlow.Adapters.Api` has no direct dependency on Domain, Infrastructure, Host, or SharedKernel.
- [ ] `GarageFlow.Domain` contains no `Repositories` folder and no read models.
- [ ] Application owns ports, read models, use cases, and command transaction behavior.
- [ ] Infrastructure implements Application ports and query ports.
- [ ] API enum contracts no longer expose Domain enum types.
- [ ] Architecture tests enforce the new dependency model.
- [ ] Unit, integration, E2E, and solution-level tests pass.
