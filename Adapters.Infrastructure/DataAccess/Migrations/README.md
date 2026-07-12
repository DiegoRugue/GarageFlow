# GarageFlow Migrations

`GarageFlowDbContext` migrations are generated in this folder.
Current baseline migration: `20260425000000_InitialCustomers`.

Default command:

```bash
dotnet ef migrations add <MigrationName> --project Adapters.Infrastructure/GarageFlow.Adapters.Infrastructure.csproj --startup-project Host/GarageFlow.Host.csproj --context GarageFlowDbContext --output-dir DataAccess/Migrations
```

The functional phase-2 checkpoint is represented by the deterministic migration
`20260711090000_WorkOrderFunctionalCompliance`.
