# Workforce & Competence Management

A full-stack workforce-planning and competence-management prototype for **employees, competence, shift planning and staffing coverage**.

[![Full Stack CI](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml)
[![CodeQL](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml)
[![Dependency Review](https://github.com/abla86/workforce-competence-management/actions/workflows/dependency-review.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/dependency-review.yml)

## Portfolio summary

This project demonstrates full-stack application engineering around a realistic workforce-management problem: combining employee competence, availability and shift requirements to support safer staffing decisions.

The application is deliberately presented as **decision support**, not autonomous staffing. It exposes the reasons behind coverage results and keeps organisational, legal and professional responsibility outside the software.

### What it demonstrates

- React 19 + Vite frontend
- ASP.NET Core / .NET 10 Minimal API
- Entity Framework Core 10
- SQL Server 2022
- Authentication with HTTP-only cookies and JWT validation
- Role-aware mutation controls
- Competence catalogue and employee competence records
- Shift planning and competence requirements
- Staffing and competence coverage analysis
- Candidate ranking and what-if scenarios
- Availability, absence, overlap and rest-period checks
- Audit events and coverage history
- CSV/JSON/ICS/report export and controlled import/migration flows
- Docker and Docker Compose
- GitHub Actions CI/CD
- CodeQL, Dependabot and dependency review
- Automated backend, frontend and full-stack verification

## Live demonstration

**Live demo:** https://workforce-frontend.onrender.com

The deployment is a portfolio demonstration using demo data and automatic demo authentication. **Do not enter real employee, health, confidential or other sensitive information.**

Useful entry points:

- [User guide](docs/USER-GUIDE.md)
- [API guide](docs/API.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Security](README-SECURITY.md)
- [Production readiness](docs/PRODUCTION-READINESS.md)
- [Test matrix](docs/TEST-MATRIX.md)
- [Portfolio evidence](docs/PORTFOLIO.md)

## Core functionality

### Workforce and competence

- Employee records with role, department and employment percentage
- Competence catalogue
- Basic / Intermediate / Advanced competence levels
- Validity and expiry tracking
- Review-due indicators
- Competence requirements linked to shifts

### Shift planning

- Day/evening/night shifts
- Date, start time and duration
- Minimum staffing
- Employee assignment/removal
- Required competence, level, count and role
- Critical requirements
- Availability and absence checks
- Overlap checks
- Working-time warning baselines

### Staffing decision support

Coverage is represented as:

| Status | Meaning |
|---|---|
| **GREEN** | Configured staffing and competence requirements are satisfied |
| **YELLOW** | Non-critical warnings or gaps require review |
| **RED** | Minimum staffing or a critical competence requirement is not satisfied |

The interface also exposes the underlying reasons rather than relying on colour alone.

Candidate ranking supports:

- competence matching
- required role checks
- absence checks
- overlapping-shift checks
- working-time warnings
- replacement planning
- what-if coverage scenarios

The system is **decision support**. It does not replace professional judgement, legislation, collective agreements, local staffing rules or organisational responsibility.

## Working-time safety baseline

The prototype centralises its scheduling baseline in `SchedulingRules.cs` and currently uses:

- 11 hours minimum daily rest baseline
- 35 hours minimum weekly rest baseline
- 37.5 hours default weekly-hours baseline
- 24 hours maximum shift-duration guardrail

These values are **software baselines for the prototype**, not a declaration of legal compliance. Applicable legislation, collective agreements, local policies and documented exceptions must always be assessed for the target organisation.

## Architecture

```text
React 19 + Vite
        |
        v
Shared frontend API client
        |
        v
ASP.NET Core Minimal API (.NET 10)
        |
        +--> Authentication / Authorization
        +--> CoverageService
        +--> PlanningAdvisor
        +--> SchedulingRules
        +--> Audit / Data Exchange
        |
        v
Entity Framework Core 10
        |
        v
SQL Server 2022
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for boundaries, security controls, data flow and operational assumptions.

## Security

Implemented controls include:

- HTTP-only authentication cookie
- JWT issuer, audience, lifetime and signing-key validation
- role-aware mutation protection
- authentication rate limiting
- account lockout after repeated failed logins
- fixed-time comparison for bootstrap credentials
- CORS allow-list configuration
- audit events
- security response headers
- CodeQL
- Dependabot
- dependency review
- non-root API container
- controlled file-upload size limits
- server-side import/migration validation
- explicit production TLS/secure-cookie requirements

The local/demo configuration intentionally allows an insecure localhost cookie setting where required for the HTTP development stack. Production deployment requires TLS, secure cookies, production secret management and an appropriate privacy/security review.

See [README-SECURITY.md](README-SECURITY.md).

## Data exchange

The frontend/API supports controlled export and migration workflows, including:

- employee CSV
- competence CSV
- shift-plan export
- JSON backup export
- ICS calendar export
- standalone HTML shift-plan report
- browser print / Save as PDF
- structured JSON/CSV inspection
- server-side migration validation

These features are **not** a substitute for an organisation's controlled backup, retention, access-control and recovery procedures.

## Testing and verification

The backend test suite currently contains **19 xUnit tests** across coverage, availability and planning behaviour.

The repository also validates:

- EF Core migration/model state
- backend build and tests
- frontend lint and production build
- Docker Compose configuration and build
- API health
- frontend health
- unauthenticated API protection
- demo authentication
- employee/competence/shift CRUD flow
- coverage analysis
- candidate retrieval
- audit access
- coverage scenario evaluation

The authoritative current CI result is the GitHub Actions workflow linked by the badge above. This README intentionally does not claim that a historical run proves the current repository state.

See [docs/TEST-MATRIX.md](docs/TEST-MATRIX.md).

## Local development

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker Desktop
- PowerShell

### Full-stack verification

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-local-stack.ps1"
```

### Backend

```powershell
dotnet restore backend/Workforce.Api.Tests/Workforce.Api.Tests.csproj
dotnet build backend/Workforce.Api.Tests/Workforce.Api.Tests.csproj -c Release
dotnet test backend/Workforce.Api.Tests/Workforce.Api.Tests.csproj -c Release
```

### Frontend

```powershell
cd frontend
npm ci
npm run lint
npm run build
```

### Docker

```powershell
docker compose config --quiet
docker compose up --build
```

The local API and frontend ports are documented in the repository's deployment and user guides.

## Database migrations

The API uses **EF Core migrations**, not `EnsureCreated()`.

For a deliberate model change:

```powershell
dotnet ef migrations add <DescriptiveName> --project backend/Workforce.Api --startup-project backend/Workforce.Api
```

Migration/model validation is part of CI. A migration mismatch should be corrected rather than suppressed.

## Production boundary

This repository is a **portfolio prototype**, not a production healthcare or workforce-management product.

Before production use, the target organisation would need to establish, at minimum:

- appropriate identity and access management
- TLS and secure secret management
- privacy and data-protection assessment
- retention and deletion policy
- backup and disaster recovery
- monitoring and incident response
- formal validation of working-time rules
- accessibility and usability validation
- organisation-specific security testing
- operational ownership and change control

See [docs/PRODUCTION-READINESS.md](docs/PRODUCTION-READINESS.md).

## Technology

| Area | Technology |
|---|---|
| Frontend | React 19, Vite 7, JavaScript |
| Backend | C#, ASP.NET Core / .NET 10 |
| API | Minimal API, OpenAPI |
| Database | SQL Server 2022, EF Core 10 |
| Authentication | JWT validation, HTTP-only cookie |
| Password hashing | BCrypt.Net-Next |
| Documents | Open XML |
| Testing | xUnit, automated smoke verification |
| Containers | Docker, Docker Compose |
| CI/CD | GitHub Actions |
| Security | CodeQL, Dependabot, dependency review |

## Repository evidence

The project includes documentation intended to make engineering decisions inspectable:

- architecture documentation
- user and API guides
- security documentation
- production-readiness boundary
- functional test matrix
- deployment guide
- upgrade guide
- portfolio evidence
- worklog
- repository change-control audit

## Author

**Anne Beth Andersen**

Portfolio: https://abla86.github.io/developer-portfolio/

## Scope and responsible use

The project uses synthetic/demo data. Do not use real employee, health or other sensitive personal data in the portfolio deployment.

The application provides decision support and transparent explanations. It must not be presented as an automated system for making employment, staffing or clinical decisions without appropriate human oversight and organisational validation.
