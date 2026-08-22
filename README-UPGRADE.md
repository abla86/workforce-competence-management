# Workforce & Competence Management — upgrade history and verification

This document records major interactive upgrades to the prototype. It is not a separate application or a second source of truth for the current feature set; `README.md` is the authoritative current overview.

## Current interactive scope

### Employees

- Create employee
- Edit name, role, position percentage and active status
- Delete employee when historical shift assignments do not prevent deletion
- Assign/update competence
- Set competence level
- Set validity date
- Remove competence

### Competence

- Create competence
- View competence coverage across the workforce
- Identify missing competence
- Identify active/review-due/expired competence
- Delete unused competence

### Shifts and staffing

- Create shift
- Delete shift
- Assign employees
- Remove employees from shift
- Define minimum staffing
- Add/update competence requirements
- Remove competence requirements
- Recalculate staffing and competence coverage
- Display GREEN / YELLOW / RED operational status
- Explain staffing and competence gaps

### Planning support

- Candidate ranking for eligible replacement employees
- Availability and absence checks
- Overlapping-shift detection
- Rest-period warnings
- Non-destructive what-if scenario analysis
- Coverage evaluation history through audit events

### Data exchange

- Employee CSV export/import
- Competence CSV export/import
- Shift-plan spreadsheet-compatible export
- JSON backup export
- HTML shift-plan sharing
- ICS calendar export
- Browser Print / Save as PDF

### Dashboard

- Operational staffing overview
- Competence coverage
- Action-required shifts
- Competence expiry/review alerts
- Shift-level coverage explanations

## Database migrations

The prototype uses a checked-in EF Core migration set instead of `EnsureCreated()`.

Current baseline:

- `backend/Workforce.Api/Migrations/20260822161018_InitialCreate.cs`
- `backend/Workforce.Api/Migrations/20260822161018_InitialCreate.Designer.cs`
- `backend/Workforce.Api/Migrations/AppDbContextModelSnapshot.cs`

The API applies pending migrations before seeding. CI validates the migration set and checks for pending model changes.

## Verification

The current backend test suite contains **18 xUnit tests**, all passing in the final local verification.

The frontend uses lint/build validation rather than a separate automated component/E2E test suite.

The complete local verification also validates the EF model against the checked-in snapshot, Docker Compose configuration/build/start, SQL Server health, API health and frontend availability.

## CI smoke verification

GitHub Actions validates the complete Docker stack by:

1. building the Compose images;
2. starting SQL Server, API and frontend;
3. waiting for API health;
4. validating the checked-in EF migration baseline;
5. checking frontend availability;
6. authenticating a synthetic CI/demo administrator;
7. calling protected dashboard and shift endpoints;
8. running the workforce CRUD/planning smoke flow;
9. printing service logs on failure; and
10. tearing the stack down with its temporary database volume.

The CI data is synthetic and disposable. It must never contain real employee or patient information.

## Platform maintenance

The backend targets .NET 10, with Microsoft ASP.NET Core and EF Core packages aligned to the 10.0.11 patch release used by the current project files.

The CodeQL workflow uses the current repository configuration. Dependabot is configured through `.github/dependabot.yml`.

## Important distinction

The current implementation combines employee/competence/shift management with coverage evaluation, candidate planning, scenario analysis, authentication, audit events and data exchange.

For the authoritative current feature list and architecture, see `README.md`.
