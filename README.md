# Workforce & Competence Management

A full-stack workforce-planning and competence-management prototype for managing **employees, competence, shift plans and staffing coverage** in one system.

The application is designed as explainable decision support: it evaluates whether a planned shift has enough staff with the required roles and valid competence, identifies gaps and suggests qualified replacements. Final staffing decisions remain with an authorized human user.

[![CI](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/ci.yml)
[![CodeQL](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml/badge.svg)](https://github.com/abla86/workforce-competence-management/actions/workflows/codeql.yml)

## Core prototype areas

### 1. Shift planning

- Create and manage day, evening and night shifts
- Define shift date, duration, department and minimum staffing
- Assign and remove employees from shifts
- Define competence requirements for each shift
- Define required competence level and required count
- Define required role and critical requirements
- Detect duplicate/overlapping assignments
- Check availability, approved absence and rest-period warnings
- Calculate planned staffing against minimum staffing

### 2. Competence management

- Competence catalogue
- Employee competence records
- Competence levels: Basic, Intermediate and Advanced
- Validity/expiry dates
- Expired and review-due competence indicators
- Competence requirements linked directly to shifts
- Automatic qualification checks against required level and validity

### 3. Staffing and coverage

- Minimum staffing validation
- Competence coverage calculation
- Required-role checks
- Explainable GREEN / YELLOW / RED operational status
- Human-readable reasons for uncovered requirements
- Candidate ranking for replacement planning
- What-if scenario analysis without modifying the real assignment
- Suggested replacement candidates
- Coverage evaluation history through the audit-event store

## Decision model

- **GREEN** — staffing and configured competence requirements are covered and no blocking availability issue is detected.
- **YELLOW** — the shift is not fully covered by all configured requirements, but the gap is non-critical and requires review.
- **RED** — minimum staffing is not met or a critical competence requirement is missing.

The application displays the underlying reasons instead of relying on colour alone.

## Coverage checks

1. Minimum staffing
2. Required competence
3. Minimum competence level
4. Competence validity at the shift date
5. Required role
6. Approved absence
7. Double booking
8. Rest-period warning
9. Candidate ranking for replacement planning

## Scenario analysis

`POST /api/shifts/{id}/coverage/scenario` can temporarily remove one or more employee IDs from a shift simulation. The real database assignments are not changed by the simulation.

The result contains:

- simulated staffing coverage
- simulated competence coverage
- warnings and gaps
- eligible replacement candidates

This is decision support, not automatic scheduling.

## Data exchange

The current backend also provides authenticated data-exchange functions for practical administration:

- Employee CSV export
- Competence CSV export
- Shift-plan spreadsheet-compatible export
- JSON backup export
- HTML shift-plan sharing view
- Employee CSV import
- Competence CSV import

These functions are intended for controlled development/demo use and must be subject to the organization's information-security and privacy requirements if adapted for real employee data.

## API areas

### Authentication

- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `POST /api/auth/bootstrap`

### Workforce and competence

- `GET /api/employees`
- `POST /api/employees`
- `PUT /api/employees/{id}`
- `DELETE /api/employees/{id}`
- `POST /api/employees/{id}/competences`
- `DELETE /api/employees/{id}/competences/{competenceId}`
- `GET /api/competences`
- `POST /api/competences`
- `DELETE /api/competences/{id}`

### Shift planning

- `GET /api/shifts`
- `POST /api/shifts`
- `PUT /api/shifts/{id}`
- `DELETE /api/shifts/{id}`
- `POST /api/shifts/{id}/assignments`
- `DELETE /api/shifts/{id}/assignments/{employeeId}`
- `POST /api/shifts/{id}/requirements`
- `DELETE /api/shifts/{id}/requirements/{competenceId}`

### Coverage and planning support

- `GET /api/shifts/{id}/coverage`
- `POST /api/shifts/{id}/coverage/scenario`
- `GET /api/shifts/{id}/coverage/history`
- `GET /api/shifts/{id}/candidates`
- `POST /api/scenarios/absence`

### Health

- `GET /health`

## Frontend

The React application provides dedicated views for:

- Dashboard — operational staffing and competence overview
- Employees — employee and competence administration
- Competence — competence catalogue and coverage
- Shifts — shift planning, assignments, requirements and live coverage
- Gap Analysis — identification of staffing and competence gaps

The shift-management view combines staffing, competence requirements, coverage status, candidate ranking and what-if analysis in the same workflow.

## Architecture

```text
React + Vite frontend
        |
        v
ASP.NET Core Minimal API
        |
        +-- CoverageService
        +-- PlanningAdvisor
        +-- Authentication / RBAC
        +-- Audit events
        +-- Data import / export
        |
        v
Entity Framework Core
        |
        v
SQL Server
```

The existing `ShiftAssignment` + `ShiftRequirement` model remains the authoritative scheduling model. A separate task-coverage model is deliberately not introduced, avoiding competing sources of truth.

## Security

- JWT authentication stored in an HTTP-only cookie
- Role checks for mutating planning data
- Login/bootstrap rate limiting
- Account lockout after repeated failed login attempts
- CORS configuration
- Audit events
- CodeQL workflow
- Dependabot configuration

Production deployment still requires HTTPS, secure cookie configuration, real secret management, database backup/recovery controls, identity-management hardening and an appropriate privacy/security assessment before real employee data is used.

See [README-SECURITY.md](README-SECURITY.md).

## Testing and CI

The backend contains an xUnit test project covering core coverage rules and planning constraints. GitHub Actions validates:

- backend restore/build/test
- frontend npm install/lint/build
- Docker Compose configuration and image build

The repository should not be described as production-ready merely because the source builds. End-to-end execution against SQL Server and a green CI run are required before a production claim is justified.

Run backend tests locally:

```bash
dotnet test backend/Workforce.Api.Tests/Workforce.Api.Tests.csproj --configuration Release
```

Run frontend checks:

```bash
cd frontend
npm ci
npm run lint
npm run build
```

## Run with Docker Compose

Create `.env` from `.env.example` and provide local development values for:

- `DB_PASSWORD`
- `JWT_SECRET_KEY`
- `VAKTKLAR_BOOTSTRAP_KEY`

Then:

```bash
docker compose up --build
```

Local addresses:

- Frontend: `http://localhost:8088`
- API: `http://localhost:5080`
- Health: `http://localhost:5080/health`

## Data safety

All repository demo data must be fictional. Do not store real employee, patient or other sensitive personal information in the repository.

## Project purpose

The prototype demonstrates full-stack engineering around a realistic workforce-management problem: converting staffing, competence and availability rules into transparent operational decision support.

## Related prototype

The repository is the web-based/full-stack prototype. A separate C#/.NET **Shift & Competence Planner** demonstrates the same domain at a smaller scope. Keeping the two repositories separate makes the progression from a focused planning prototype to the full-stack system visible in the portfolio.

## Author

Anne Beth Andersen

## Portfolio

https://abla86.github.io/developer-portfolio/
