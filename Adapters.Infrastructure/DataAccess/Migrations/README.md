# GarageFlow Migrations

`GarageFlowDbContext` migrations are generated in this folder.
Current baseline migration: `20260425000000_InitialCustomers`.

Default command:

```bash
dotnet ef migrations add <MigrationName> --project Infrastructure --startup-project Api --context GarageFlowDbContext --output-dir DataAccess/Migrations
```
