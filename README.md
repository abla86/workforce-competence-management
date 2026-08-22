# Workforce & Competence Management

A full-stack workforce-planning and competence-management prototype for **employees, competence, shift planning and staffing coverage**.

[![CI](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml)
[![CodeQL](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml)

## Prototype status

**Prototype 2 — verified runnable full-stack prototype.**

The local stack has been end-to-end verified after the final EF migration/model correction:

- **18/18 backend tests passed**
- **EF model and migration/snapshot validation passed** with no pending model changes
- **Frontend lint and production build passed**
- **Docker Compose build passed**
- **SQL Server healthy**
- **ASP.NET Core API healthy**
- **Frontend HTTP health passed**
- **Demo authentication verified successfully**

The repository is suitable for local demonstrations, controlled internal testing and portfolio presentation. It is **not production-ready** until the controls in [Production Readiness](docs/PRODUCTION-READINESS.md) are completed for the target organisation.

## Live demo

**[Open Workforce & Competence Management](https://workforce-frontend.onrender.com)**

The portfolio deployment uses a demo datastore and automatic demo login. It is intended for demonstrations and portfolio review only. **Do not enter real employee, health, confidential or other sensitive data.**

The live deployment is configured in [`render.yaml`](render.yaml) and is automatically redeployed from `main` when the Render Blueprint is connected to this repository.

## Local demo

After the verification script has completed successfully:

- Frontend: **http://localhost:8088**
- API: **http://localhost:5080**
- Health: **http://localhost:5080/health**
- OpenAPI: **http://localhost:5080/openapi/v1.json**

### Demo login

When `DEMO_MODE=true` and the database has no user account, the seed process creates the local demo account.

The local Docker Compose configuration supplies the demo credentials for this non-production environment. Do not reuse demo credentials in production.

## What the prototype does

### Shift planning

- Day/evening/night shifts
- Date, start time, duration and department
- Minimum staffing
- Employee assignment/removal
- Shift competence requirements
- Required level/count/role
- Critical requirements
- Overlap and availability checks
- Absence and rest-period checks
- Live coverage analysis

### Competence management

- Competence catalogue
- Employee competence records
- Basic / Intermediate / Advanced levels
- Validity/expiry tracking
- Expired/review-due indicators
- Competence requirements directly linked to shifts

### Staffing decision support

- Minimum staffing evaluation
- Competence coverage
- Required-role checks
- GREEN / YELLOW / RED operational status
- Human-readable gap explanations
- Candidate ranking
- Replacement planning
- What-if analysis
- Absence scenario simulation
- Coverage history/audit events

The system is decision support. It does not replace professional judgement or local staffing rules.

## Data & Reports workspace

The frontend includes a dedicated **Data & Reports** view:

- JSON backup export
- Employee CSV export
- Competence CSV export
- Shift-plan CSV export
- ICS calendar export
- Standalone HTML shift-plan report
- Browser Print / Save as PDF
- Controlled JSON import for employees and competences

These are browser-side exports of the authenticated dataset. They are not a replacement for a controlled production backup system.

## Status model

| Status | Meaning |
|---|---|
| GREEN | Configured staffing and competence requirements are satisfied |
| YELLOW | Non-critical warnings/gaps require review |
| RED | Minimum staffing or a critical competence requirement is not satisfied |

The application exposes the reasons behind the status instead of relying on colour alone.

## API

The ASP.NET Core OpenAPI document is available locally at:

`http://localhost:5080/openapi/v1.json`

Core endpoints include:

- `/api/auth/*`
- `/api/employees`
- `/api/competences`
- `/api/shifts`
- `/api/shifts/{id}/coverage`
- `/api/shifts/{id}/coverage/scenario`
- `/api/shifts/{id}/candidates`
- `/api/scenarios/absence`
- `/api/absences`
- `/api/dashboard`
- `/api/audit`
- `/health`

See [docs/API.md](docs/API.md) for the capability map.

## Architecture

```text
React + Vite
    ↓
ASP.NET Core Minimal API (.NET 10)
    ↓
CoverageService / PlanningAdvisor / Authentication / Audit
    ↓
Entity Framework Core 10
    ↓
SQL Server
```

`ShiftAssignment` and `ShiftRequirement` are the authoritative scheduling model.

## Languages, data languages and configuration

The repository should not be described as if every GitHub language entry were a programming language. The current implementation uses several distinct categories:

### Programming languages

- **C#** — backend/API, domain and planning logic
- **JavaScript** — React frontend
- **PowerShell** — local verification and automation scripts

GitHub currently identifies **C# as the repository's primary language**. GitHub's language classification is separate from the broader technology stack below.

### Data/query language

- **SQL/T-SQL** — SQL Server database operations and relational data work, primarily through Entity Framework Core and SQL Server tooling

### Markup, styling and configuration

- **HTML**
- **CSS**
- **YAML** — GitHub Actions and configuration
- **JSON** — API/data/configuration payloads
- **Dockerfile** — container image definition
- **Docker Compose YAML** — local multi-container orchestration

### Frameworks and libraries

- React 19
- Vite 7
- ASP.NET Core / .NET 10
- Entity Framework Core 10
- OpenAPI
- xUnit
- BCrypt.Net-Next

### Database and infrastructure

- SQL Server 2022 container image
- Docker
- Docker Compose

### CI/CD and security tooling

- GitHub Actions
- CodeQL
- Dependabot
- Automated backend/frontend/Docker smoke verification

## Database schema management

The API uses **EF Core migrations**, not `EnsureCreated()`. The current `InitialCreate` migration and model snapshot are checked into `backend/Workforce.Api/Migrations/` and were regenerated from the current model. The verified model uses an index-compatible length for `Competence.Name` rather than `nvarchar(max)`.

On startup, pending migrations are applied before seed data is inserted. An `IDesignTimeDbContextFactory<AppDbContext>` is used for EF Core CLI operations so design-time tooling does not depend on application authentication secrets.

For future model changes:

```bash
dotnet ef migrations add <DescriptiveName> --project backend/Workforce.Api --startup-project backend/Workforce.Api
```

After a model change, the migration must be regenerated/updated and the complete verification workflow must pass before the change is considered complete.

## Security

- HTTP-only authentication cookie
- Authentication required for API routes
- Role-aware mutation controls
- Login/bootstrap rate limiting
- Account lockout
- CORS configuration
- Audit events
- Security response headers
- CodeQL
- Dependabot
- Non-root API container

The Docker demo uses HTTP localhost and therefore `SECURITY_COOKIE_SECURE=false`. Production requires TLS, secure cookies, production secret management and an appropriate identity/privacy/security review.

See [README-SECURITY.md](README-SECURITY.md), [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) and [docs/PRODUCTION-READINESS.md](docs/PRODUCTION-READINESS.md).

## Testing and CI

GitHub Actions is configured to verify:

- .NET 10 restore/build/test
- EF Core migration set and pending-model check
- frontend `npm ci`, lint and build
- Docker Compose configuration/build/start
- SQL Server health
- API health
- frontend HTTP availability
- bootstrap/login
- authenticated dashboard and shifts endpoints
- workforce CRUD/planning smoke flow
- CodeQL analysis

The backend currently contains **18 xUnit tests**, all of which passed in the final local verification. The frontend has lint/build validation; a dedicated component/E2E suite is not currently claimed.

See [docs/TEST-MATRIX.md](docs/TEST-MATRIX.md).

## Run locally

Use the repository's single verification script for a clean end-to-end local verification. It resets to the current `main`, restores/builds/tests the backend, validates the EF model against the checked-in migration snapshot, validates the frontend, rebuilds the Docker stack, waits for SQL Server/API/frontend health and prints the local URLs only after those checks pass.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-local-stack.ps1"
```

Do not bypass the verification workflow by suppressing EF pending-model warnings. A migration/model mismatch must be corrected rather than hidden.

## Documentation

- [User Guide](docs/USER-GUIDE.md)
- [API Guide](docs/API.md)
- [Data Formats](docs/DATA-FORMATS.md)
- [Functional Test Matrix](docs/TEST-MATRIX.md)
- [Deployment Guide](docs/DEPLOYMENT.md)
- [Production Readiness](docs/PRODUCTION-READINESS.md)
- [Security](README-SECURITY.md)
- [Upgrade Guide](README-UPGRADE.md)

## Prototype boundary

This is a runnable full-stack prototype suitable for local development, demonstrations, controlled internal testing and portfolio presentation. It is **not claimed to be production-ready** until identity/MFA, production secret management, backup/recovery, granular RBAC, audit attribution, privacy assessment, security review, observability and deployment controls have been completed for the target environment.

## Author

Anne Beth Andersen

## Portfolio

https://abla86.github.io/developer-portfolio/
