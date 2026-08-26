# Workforce & Competence Management

A full-stack workforce-planning and competence-management prototype for **employees, competence, shift planning and staffing coverage**.

[![CI](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml)
[![CodeQL](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml)

## Start here

- **Live demo:** https://workforce-frontend.onrender.com
- **User guide:** [docs/USER-GUIDE.md](docs/USER-GUIDE.md)
- **API guide:** [docs/API.md](docs/API.md)
- **Test matrix:** [docs/TEST-MATRIX.md](docs/TEST-MATRIX.md)
- **Security:** [README-SECURITY.md](README-SECURITY.md)
- **Production readiness:** [docs/PRODUCTION-READINESS.md](docs/PRODUCTION-READINESS.md)
- **Worklog:** [PORTFOLIO-WORKLOG.md](PORTFOLIO-WORKLOG.md)

## Status at a glance

**Prototype 2 — verified runnable full-stack prototype.**

The repository is suitable for local demonstrations, controlled internal testing and portfolio presentation. It is **not claimed to be production-ready** until the controls in [Production Readiness](docs/PRODUCTION-READINESS.md) are completed for the target organisation.

### Verification evidence

The final local verification recorded:

- **18/18 backend tests passed**
- EF model/migration validation passed with no pending model changes
- frontend lint and production build passed
- Docker Compose build passed
- SQL Server health passed
- ASP.NET Core API health passed
- frontend HTTP health passed
- demo authentication verified

These results demonstrate the tested software behaviour. They do not establish suitability for a particular employer, staffing policy, clinical service or production environment.

## Live demo

**[Open Workforce & Competence Management](https://workforce-frontend.onrender.com)**

The live deployment uses a demo datastore and automatic demo login. It is intended for demonstrations and portfolio review only. **Do not enter real employee, health, confidential or other sensitive data.**

## What the prototype does

### Shift planning

- day/evening/night shifts
- date, start time, duration and department
- minimum staffing
- employee assignment/removal
- shift competence requirements
- required level/count/role
- critical requirements
- overlap and availability checks
- absence and rest-period checks
- live coverage analysis

### Competence management

- competence catalogue
- employee competence records
- Basic / Intermediate / Advanced levels
- validity/expiry tracking
- expired/review-due indicators
- competence requirements linked to shifts

### Staffing decision support

- minimum staffing evaluation
- competence coverage
- required-role checks
- GREEN / YELLOW / RED operational status
- human-readable gap explanations
- candidate ranking
- replacement planning
- what-if analysis
- absence scenario simulation
- coverage history/audit events

The system is **decision support**. It does not replace professional judgement, collective agreements, local staffing rules or organisational responsibility.

### Resilience and safe integration

The frontend uses one shared API communication layer for the application's data flow. Read-only requests can recover automatically from transient network failures and selected gateway/service-limit responses, while state-changing requests are deliberately **not** retried automatically to avoid duplicate writes. Request timeouts and structured error metadata, including an API request identifier when provided by the server, are surfaced to the UI for diagnosis.

This is resilience against transient technical failure; it is **not** a claim of automatic recovery from application defects, data corruption or infrastructure failure.

## Data & Reports

The frontend includes:

- JSON backup export
- employee CSV export
- competence CSV export
- shift-plan CSV export
- ICS calendar export
- standalone HTML shift-plan report
- browser print / Save as PDF
- controlled JSON import for employees and competences

These are browser-side exports of the authenticated dataset. They are not a replacement for a controlled production backup/recovery system.

## Status model

| Status | Meaning |
|---|---|
| GREEN | Configured staffing and competence requirements are satisfied |
| YELLOW | Non-critical warnings/gaps require review |
| RED | Minimum staffing or a critical competence requirement is not satisfied |

The application exposes the reasons behind the status instead of relying on colour alone.

## Architecture

```text
React + Vite
    ↓
Shared frontend API client with controlled recovery
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

## Technology actually represented

### Programming

- C# — backend/API, domain and planning logic
- JavaScript — React frontend
- PowerShell — local verification and automation scripts

### Data / database

- SQL Server 2022
- Entity Framework Core 10

The repository does not currently contain standalone SQL/T-SQL source files; SQL Server is accessed through EF Core and related tooling.

### Frameworks / tooling

- React 19
- Vite 7
- ASP.NET Core / .NET 10
- Entity Framework Core 10
- OpenAPI
- xUnit
- BCrypt.Net-Next
- Docker / Docker Compose
- GitHub Actions
- CodeQL
- Dependabot

## Database schema management

The API uses **EF Core migrations**, not `EnsureCreated()`. The checked-in migration and model snapshot are validated by the verification workflow.

For future model changes:

```bash
dotnet ef migrations add <DescriptiveName> --project backend/Workforce.Api --startup-project backend/Workforce.Api
```

After a model change, the migration/model state and complete verification workflow must pass before the change is considered complete.

## Security boundary

Implemented controls include HTTP-only authentication cookies, authenticated API routes, role-aware mutation controls, login/bootstrap rate limiting, account lockout, CORS configuration, audit events, security response headers, CodeQL, Dependabot and a non-root API container.

The Docker demo uses HTTP localhost and therefore `SECURITY_COOKIE_SECURE=false`. Production requires TLS, secure cookies, production secret management and an appropriate identity/privacy/security review.

See [README-SECURITY.md](README-SECURITY.md), [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) and [docs/PRODUCTION-READINESS.md](docs/PRODUCTION-READINESS.md).

## Testing and CI

GitHub Actions verifies the documented backend, frontend and Docker workflow, including EF migration state, health checks, authentication and workforce smoke flows, plus CodeQL analysis.

The backend currently contains **18 xUnit tests**, all of which passed in the final local verification. The frontend has lint/build validation; a dedicated component/E2E suite is not claimed.

See [docs/TEST-MATRIX.md](docs/TEST-MATRIX.md).

## Run locally

Use the repository's verification script for end-to-end local verification:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-local-stack.ps1"
```

Do not suppress EF pending-model warnings. A migration/model mismatch must be corrected rather than hidden.

## Documentation

- [User Guide](docs/USER-GUIDE.md)
- [API Guide](docs/API.md)
- [Data Formats](docs/DATA-FORMATS.md)
- [Functional Test Matrix](docs/TEST-MATRIX.md)
- [Deployment Guide](docs/DEPLOYMENT.md)
- [Production Readiness](docs/PRODUCTION-READINESS.md)
- [Security](README-SECURITY.md)
- [Upgrade Guide](README-UPGRADE.md)
- [Worklog](PORTFOLIO-WORKLOG.md)

## Employer / portfolio evidence

This project demonstrates:

- full-stack application development
- modelling of competence and staffing rules
- REST API and database engineering
- automated testing and verification
- resilient frontend/API communication with safe retry boundaries
- Docker and CI/CD workflows
- explicit security and production boundaries
- decision-support design that exposes reasons rather than hiding them behind a status colour

## Author

Anne Beth Andersen

## Portfolio

https://abla86.github.io/developer-portfolio/
