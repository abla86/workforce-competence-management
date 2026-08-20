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

### Backend

```powershell
dotnet test .\backend\Workforce.Api.Tests\Workforce.Api.Tests.csproj --configuration Release
```

The current backend test suite contains 11 unit tests.

### Frontend

```powershell
cd .\frontend
npm ci
npm run lint
npm run build
```

The frontend currently uses lint/build validation rather than a separate automated component/E2E test suite.

### Complete development stack

```powershell
cd ..
docker compose up --build
```

The stack exposes the frontend on `http://localhost:8088`, the API on `http://localhost:5080` and OpenAPI on `http://localhost:5080/openapi/v1.json`.

### CI smoke verification

GitHub Actions now validates the complete Docker stack by:

1. building the Compose images;
2. starting SQL Server, API and frontend;
3. waiting for the API database health endpoint;
4. checking the frontend response;
5. bootstrapping a temporary CI administrator;
6. logging in and storing the HTTP-only authentication cookie;
7. calling protected dashboard and shift endpoints;
8. printing service logs on failure; and
9. tearing the stack down with its temporary database volume.

## Platform maintenance

The backend has been migrated from .NET 9 to supported .NET 10, and the Microsoft ASP.NET Core/EF Core packages are aligned to the current 10.0.11 patch release used for this audit.

The CodeQL workflow has been migrated from CodeQL Action v3 to v4. GitHub's current CodeQL documentation lists v4 as the latest supported major version.

## Important distinction

The prototype is functionally broader than the original V2 CRUD upgrade. The current implementation combines the original employee/competence/shift management with coverage evaluation, candidate planning, scenario analysis, authentication, audit events and data exchange.

For the authoritative current feature list and architecture, see `README.md`.
