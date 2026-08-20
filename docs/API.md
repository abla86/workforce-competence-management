# API Capability Guide

The API is an ASP.NET Core REST API backed by EF Core and SQL Server. All `/api/*` routes require authentication except `/api/auth/*`.

## Employees

- `GET /api/employees` — search/filter employees and return competence/absence status
- `POST /api/employees` — create employee
- `PUT /api/employees/{id}` — update employee
- `DELETE /api/employees/{id}` — delete only when historical assignments permit it
- `POST /api/employees/{id}/competences` — add/update competence
- `DELETE /api/employees/{id}/competences/{competenceId}` — remove competence

## Competence

- `GET /api/competences`
- `POST /api/competences`
- `DELETE /api/competences/{id}`

## Shifts

- `GET /api/shifts` — shifts with calculated coverage status
- `POST /api/shifts`
- `PUT /api/shifts/{id}`
- `DELETE /api/shifts/{id}`
- `POST /api/shifts/{id}/assignments`
- `DELETE /api/shifts/{id}/assignments/{employeeId}`
- `POST /api/shifts/{id}/requirements`
- `DELETE /api/shifts/{id}/requirements/{competenceId}`
- `GET /api/shifts/{id}/candidates`
- `GET /api/shifts/{id}/coverage`
- `POST /api/shifts/{id}/coverage/scenario`
- `GET /api/shifts/{id}/coverage/history`

## Absence and scenario planning

- `POST /api/absences`
- `GET /api/absences`
- `DELETE /api/absences/{id}`
- `POST /api/scenarios/absence`

## Operational views

- `GET /api/dashboard`
- `GET /api/audit`
- `GET /health`
- `GET /openapi/v1.json`

## Authentication

The application supports a controlled bootstrap/login flow. Authentication uses an HTTP-only cookie. Authentication endpoints are rate-limited.

## Decision-support semantics

Coverage responses expose staffing and competence results separately so that a consumer can explain why a shift is GREEN, YELLOW or RED rather than treating the status as an opaque score.

## Error handling

Expected validation and domain failures use appropriate HTTP responses including `400`, `401`, `404`, `409` and `429`.
