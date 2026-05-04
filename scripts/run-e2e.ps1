$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Resolve-Path (Join-Path $scriptDirectory "..")

Set-Location $repositoryRoot

Write-Host "Checking Docker availability..."
docker version | Out-Null

Write-Host "Restoring and building GarageFlow solution..."
dotnet restore ".\GarageFlow.slnx"
dotnet build ".\GarageFlow.slnx" --no-restore

Write-Host "Restoring and building E2E test project..."
dotnet restore ".\Tests\E2E\GarageFlow.Tests.E2E.csproj"
dotnet build ".\Tests\E2E\GarageFlow.Tests.E2E.csproj" --no-restore

Write-Host "Running E2E tests..."
dotnet test ".\Tests\E2E\GarageFlow.Tests.E2E.csproj" --no-build --logger "trx;LogFileName=e2e.trx"
