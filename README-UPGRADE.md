# Workforce & Competence Management — upgrade history and verification

This document records the major interactive upgrades to the prototype. It is not a separate application or a second source of truth for the current feature set; `README.md` is the authoritative current overview.

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

### Dashboard

- Operational staffing overview
- Competence coverage
- Action-required shifts
- Competence expiry/review alerts
- Shift-level coverage explanations

## Verification

Run backend tests:

```powershell
dotnet test .\backend\Workforce.Api.Tests\Workforce.Api.Tests.csproj --configuration Release
```

Run frontend checks:

```powershell
cd .\frontend
npm ci
npm run lint
npm run build
```

Run the complete development stack:

```powershell
cd ..
docker compose up --build
```

## Important distinction

The prototype is functionally broader than the original V2 CRUD upgrade. The current implementation combines the original employee/competence/shift management with coverage evaluation, candidate planning, scenario analysis, authentication, audit events and data exchange.

For the authoritative current feature list and architecture, see `README.md`.
