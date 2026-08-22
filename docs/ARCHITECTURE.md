# Architecture

## Purpose

Workforce & Competence Management is a portfolio-scale full-stack prototype for workforce planning, competence management, shift planning and staffing coverage.

## System context

```text
Browser
  |
  v
React + Vite frontend
  |
  | HTTPS in hosted environments / HTTP localhost
  v
ASP.NET Core Minimal API (.NET 10)
  |
  +--> Authentication / authorization
  +--> CoverageService
  +--> PlanningAdvisor
  +--> Audit / data exchange
  |
  v
Entity Framework Core 10
  |
  v
SQL Server 2022
```

## Architectural responsibilities

### Frontend

The React frontend is responsible for:

- user interaction and workflow presentation
- client-side validation and state
- authenticated API calls
- workforce, competence and shift views
- coverage and scenario presentation
- browser-side report/export workflows

The frontend does not own staffing rules or authoritative scheduling decisions.

### API

The ASP.NET Core API is responsible for:

- HTTP/API composition
- authentication and authorization integration
- validation of incoming commands
- domain workflow orchestration
- staffing and competence decision-support services
- persistence through EF Core
- audit events
- health checks and OpenAPI

### Domain services

`CoverageService` evaluates configured staffing and competence requirements.

`PlanningAdvisor` evaluates candidate assignments and identifies hard failures and reviewable warnings.

These services keep operational decision logic separate from HTTP endpoint composition.

### Persistence

Entity Framework Core 10 provides:

- relational mapping
- migrations
- schema evolution
- database access
- relationship management

SQL Server is the authoritative persistence engine for the application.

## Security architecture

The current prototype uses:

- HTTP-only authentication cookies
- JWT bearer validation behind the cookie transport
- role-aware mutation controls
- rate limiting on authentication/bootstrap operations
- account lockout
- CORS restrictions
- security response headers
- audit events
- non-root API container
- CodeQL and Dependabot

Demo credentials are supplied through environment configuration rather than source code.

The hosted portfolio deployment remains a demo environment and is not a production identity solution. Production deployment requires an enterprise identity provider/MFA, production secret management, TLS enforcement and a formal privacy/security review.

## CI/CD architecture

```text
Git push / pull request
        |
        +--> .NET restore/build/test
        +--> EF migration validation
        +--> frontend npm ci/lint/build
        +--> Docker Compose build
        +--> SQL Server/API/frontend health checks
        +--> authenticated CRUD/planning smoke flow
        +--> CodeQL
```

CI generates ephemeral test credentials for the container workflow. They are written to the GitHub Actions environment for subsequent steps and are not stored in the repository.

## Data flow: coverage decision support

```text
Employees + competences
          |
          +----> Shift requirements
          |             |
          +----> Assignments
                        |
                        v
                CoverageService
                        |
          +-------------+-------------+
          |             |             |
          v             v             v
      staffing      competence     role/critical
       checks          checks          checks
          \             |             /
           \            |            /
            +-----------+-----------+
                        |
                        v
                GREEN / YELLOW / RED
                        |
                        v
              explainable result
```

## Important design boundaries

- The system is decision support, not autonomous staffing authority.
- Coverage status is derived from configured requirements and current data.
- Human-readable reasons accompany operational status.
- Scenario analysis is non-destructive.
- Historical audit events are retained as evidence of relevant planning actions.
- Demo/test data must not be treated as real employee or health data.

## Current architectural gaps

The repository intentionally does not claim:

- Microsoft Entra ID/OIDC integration
- MFA
- production-grade secrets management
- Kubernetes deployment
- Azure infrastructure
- Infrastructure as Code
- OpenTelemetry/distributed tracing
- dedicated frontend component/E2E test suite
- production backup/recovery controls
- formal privacy impact assessment

These are separate production-readiness concerns rather than undocumented assumptions.
