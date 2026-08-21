# Workforce API

ASP.NET Core Minimal API for the Workforce & Competence Management prototype.

## Responsibilities

- Employee and competence persistence
- Shift and assignment management
- Staffing and competence requirements
- Coverage evaluation
- Candidate ranking
- What-if scenario analysis
- Authentication and role checks
- Audit events
- Data import/export functions
- OpenAPI document generation

## Technology

- .NET 10
- ASP.NET Core Minimal API
- Entity Framework Core 10
- SQL Server
- JWT authentication in an HTTP-only cookie
- xUnit v3 tests

## Run locally

```bash
dotnet restore
dotnet run
```

The API requires a SQL Server connection and the configured JWT/bootstrap secrets. See the repository `.env.example`, `docker-compose.yml` and `README-SECURITY.md`.

The API applies pending EF Core migrations before seed data is inserted.

## Database lifecycle

The repository uses checked-in EF Core migrations rather than `EnsureCreated()`.

Migration files:

- `Migrations/20260821210000_InitialCreate.cs`
- `Migrations/AppDbContextModelSnapshot.cs`

For a clean local database after an older `EnsureCreated()` version:

```bash
docker compose down -v
docker compose up --build
```

For future model changes:

```bash
dotnet ef migrations add <DescriptiveName> --project . --startup-project .
```

Review generated migrations and the model snapshot before deployment. Never delete a production database volume as a migration troubleshooting step.

## OpenAPI

When the API is running, the generated OpenAPI document is available at:

```text
http://localhost:5080/openapi/v1.json
```

## Test

```bash
dotnet test ../Workforce.Api.Tests/Workforce.Api.Tests.csproj --configuration Release
```

The current test project contains 11 unit tests covering coverage decisions and planning constraints.

## Main services

- `CoverageService` — staffing/competence evaluation and scenario analysis
- `PlanningAdvisor` — explainable candidate ranking
- `VaktklarAuthentication` — authentication, authorization guard and data exchange layer

The backend is a decision-support API. It does not replace authorized human staffing decisions.
