# Workforce & Competence Management

A full-stack workforce-planning and competence-management prototype for **employees, competence, shift planning and staffing coverage**.

[![CI](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml)
[![CodeQL](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml)

## Live demo

**[Open Workforce & Competence Management](https://workforce-frontend.onrender.com)**

The portfolio deployment uses a persistent SQL Server demo datastore and automatic demo login. It is intended for demonstrations and portfolio review only. Do not enter real employee, health, confidential or other sensitive data.

The live deployment is configured in [`render.yaml`](render.yaml) and is automatically redeployed from `main` when the Render Blueprint is connected to this repository.

## Prototype status

**Prototype 2 — functionally complete and runnable.** The repository is suitable for local demonstrations, controlled internal testing and portfolio presentation. It is not claimed to be production-ready until the controls in [Production Readiness](docs/PRODUCTION-READINESS.md) are completed for the target organisation.

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

## Database schema management

The API uses **EF Core migrations**, not `EnsureCreated()`. The initial migration and model snapshot are checked into `backend/Workforce.Api/Migrations/`. On startup, pending migrations are applied before seed data is inserted. An `IDesignTimeDbContextFactory<AppDbContext>` is included so EF Core CLI operations do not depend on application authentication secrets.

For future model changes:

```bash
dotnet ef migrations add <DescriptiveName> --project backend/Workforce.Api --startup-project backend/Workforce.Api
```

## Security

- HTTP-only authentication cookie
- Authentication required for API routes
- Role-aware mutation controls
- Login/bootstrap rate limiting
- Account lockout
- CORS configuration
- Audit events
- Security response headers
- CodeQL v4
- Dependabot
- Non-root API container

The Docker demo uses HTTP localhost and therefore `SECURITY_COOKIE_SECURE=false`. Production requires TLS, secure cookies, production secret management and an appropriate identity/privacy/security review.

See [README-SECURITY.md](README-SECURITY.md), [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) and [docs/PRODUCTION-READINESS.md](docs/PRODUCTION-READINESS.md).

## Testing and CI

GitHub Actions is configured to verify:

- .NET 10 restore/build/test
- EF Core migration set
- frontend `npm ci`, lint and build
- Docker Compose configuration/build/start
- SQL Server health
- API health
- frontend HTTP availability
- bootstrap/login
- authenticated dashboard and shifts endpoints
- workforce CRUD/planning smoke flow
- CodeQL analysis

The backend currently contains **18 xUnit tests**: 16 core coverage/planning tests plus 2 database-backed availability regression tests. The frontend has lint/build validation but not yet a dedicated component/E2E suite.

See [docs/TEST-MATRIX.md](docs/TEST-MATRIX.md).

## Run locally

Create `.env` from `.env.example` with non-production development values for:

- `DB_PASSWORD`
- `JWT_SECRET_KEY`
- `VAKTKLAR_BOOTSTRAP_KEY`
- `SECURITY_COOKIE_SECURE=false`

Then:

```bash
docker compose down -v
docker compose up --build
```

The API applies the checked-in EF Core migration before seeding the demo database.

Open:

- Frontend: `http://localhost:8088`
- API: `http://localhost:5080`
- OpenAPI: `http://localhost:5080/openapi/v1.json`
- Health: `http://localhost:5080/health`

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

## Related prototype

A separate C#/.NET **Shift & Competence Planner** demonstrates the same domain at a smaller scope. This repository is the broader full-stack Prototype 2.

## Author

Anne Beth Andersen

## Portfolio

https://abla86.github.io/developer-portfolio/
