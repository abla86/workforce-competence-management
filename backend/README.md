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

## Technology

- .NET 9
- ASP.NET Core Minimal API
- Entity Framework Core 9
- SQL Server
- JWT authentication in an HTTP-only cookie
- xUnit tests

## Run locally

```bash
dotnet restore
dotnet run
```

The API requires a SQL Server connection and the configured JWT/bootstrap secrets. See the repository `.env.example`, `docker-compose.yml` and `README-SECURITY.md`.

## Test

```bash
dotnet test ../Workforce.Api.Tests/Workforce.Api.Tests.csproj --configuration Release
```

## Main services

- `CoverageService` — staffing/competence evaluation and scenario analysis
- `PlanningAdvisor` — explainable candidate ranking
- `VaktklarAuthentication` — authentication, authorization guard and data exchange layer

The backend is a decision-support API. It does not replace authorized human staffing decisions.
