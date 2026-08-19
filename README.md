# Workforce & Competence Management

Full-stack workforce planning and competence-management prototype. The application combines employees, competence requirements, shifts, staffing coverage, candidate ranking and what-if analysis in one system.

## Current capabilities

- Employee CRUD and activation/deactivation
- Competence catalogue and employee competence records
- Competence levels: Basic, Intermediate, Advanced
- Competence validity / expiry checks
- Shift creation and management
- Minimum staffing requirements
- Competence requirements per shift
- Required-role checks
- Automatic coverage calculation
- GREEN / YELLOW / RED operational status
- Human-readable gap explanations
- Candidate ranking for safe shift assignment
- Availability checks including approved absence, overlapping shifts and rest-period warnings
- What-if scenario analysis when an assigned employee is removed
- Suggested replacement candidates
- Coverage evaluation history through the existing audit-event store
- Authentication with HTTP-only cookie JWT
- Role-based authorization for write operations
- Login/bootstrap rate limiting
- Audit events for important mutations and coverage evaluations
- Responsive React frontend
- Docker Compose development stack
- GitHub Actions CI
- CodeQL and Dependabot configuration

## Coverage engine

The coverage engine evaluates a concrete shift against its staffing and competence requirements.

### Decision model

- **GREEN** — staffing and all competence requirements are covered and no blocking availability issue is detected.
- **YELLOW** — the shift has a non-critical competence gap or warning that requires review.
- **RED** — minimum staffing is not met or a critical competence requirement is missing.

The UI also displays the underlying reason instead of relying on colour alone.

### Checks

1. Minimum staffing
2. Required competence
3. Minimum competence level
4. Competence validity at the shift date
5. Required role
6. Approved absence
7. Double booking
8. Rest-period warning
9. Candidate ranking for replacement planning

## What-if analysis

`POST /api/shifts/{id}/coverage/scenario` accepts employee IDs to remove temporarily. The database assignments are not changed by the simulation. The API returns:

- coverage after the simulated removal
- staffing and competence gaps
- warnings
- eligible replacement candidates

This is intended as a decision-support feature, not an automatic scheduling decision.

## API endpoints

### Coverage

- `GET /api/shifts/{id}/coverage`
- `POST /api/shifts/{id}/coverage/scenario`
- `GET /api/shifts/{id}/coverage/history`
- `GET /api/shifts/{id}/candidates`

### Core planning

- `GET /api/employees`
- `GET /api/competences`
- `GET /api/shifts`
- `POST /api/shifts`
- `POST /api/shifts/{id}/assignments`
- `POST /api/shifts/{id}/requirements`
- `POST /api/scenarios/absence`

## Architecture

```text
React + Vite
    |
    v
ASP.NET Core Minimal API
    |
    +-- CoverageService
    +-- PlanningAdvisor
    +-- Authentication / RBAC
    +-- Audit events
    |
    v
Entity Framework Core
    |
    v
SQL Server
```

The current implementation deliberately keeps the existing `ShiftAssignment` + `ShiftRequirement` model as the source of truth. A separate `ShiftTask` / `ShiftTaskCoverage` model is **not** included in the production branch because introducing a second scheduling model would create two competing sources of truth.

## Security

- JWT authentication stored in an HTTP-only cookie
- Role checks for mutating planning data
- Login/bootstrap rate limiting
- Account lockout after repeated failed login attempts
- CORS configuration
- Audit events
- CodeQL workflow
- Dependabot configuration

Production deployment still requires HTTPS, secure cookie configuration, real secrets, database backup/recovery controls and an appropriate privacy/security assessment before handling real employee data.

## Testing

Backend tests cover the coverage decision rules, including:

- full coverage → GREEN
- staffing shortage → RED
- non-critical competence gap → YELLOW
- critical competence gap → RED
- expired competence
- required-role mismatch

GitHub Actions verifies backend restore/build/test, frontend lint/build and Docker Compose configuration/build.

The repository should not be described as production-ready until the CI run is green and the application has been exercised end-to-end against a real SQL Server instance.

## Run locally

Create `.env` from `.env.example` and provide local development values for:

- `DB_PASSWORD`
- `JWT_SECRET_KEY`
- `VAKTKLAR_BOOTSTRAP_KEY`

Then:

```bash
docker compose up --build
```

Frontend: `http://localhost:8088`

API: `http://localhost:5080`

Health: `http://localhost:5080/health`

For local backend development:

```bash
cd backend/Workforce.Api
dotnet restore
dotnet run
```

Tests:

```bash
dotnet test backend/Workforce.Api.Tests/Workforce.Api.Tests.csproj --configuration Release
```

## Data safety

All repository demo data should be fictional. Do not place real employee, patient or other sensitive personal information in the repository.

## Project purpose

The project demonstrates full-stack development through a practical workforce-management problem: turning staffing and competence rules into explainable operational decision support.

## Author

Anne Beth Andersen

## Portfolio

This project is the featured full-stack project in the developer portfolio.

Portfolio: https://abla86.github.io/developer-portfolio/
