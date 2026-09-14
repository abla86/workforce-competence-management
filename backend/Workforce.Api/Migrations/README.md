# EF Core migrations

The application uses EF Core migrations for schema management.

## Current baseline

`20260822161018_InitialCreate` is the checked-in baseline for the current domain model. It creates the employee, competence, absence, shift, staffing, audit and authentication tables plus their indexes and relationships.

The baseline consists of:

- `20260822161018_InitialCreate.cs`
- `20260822161018_InitialCreate.Designer.cs`
- `AppDbContextModelSnapshot.cs`

## Clean local database

If a local SQL Server volume was created by an older version that used `EnsureCreated()`, remove the development volume before starting the migration-based version:

```powershell
docker compose down -v
docker compose up --build
```

Do **not** remove a production database volume as a troubleshooting step. Existing production databases require a controlled baseline/migration strategy and backup.

## Future changes

From the repository root:

```powershell
dotnet ef migrations add <DescriptiveName> --project .\backend\Workforce.Api --startup-project .\backend\Workforce.Api
```

Review both the generated migration and model snapshot, run backend tests, then deploy. The API applies pending migrations before seed data on startup.
