# Portfolio Evidence

## One-line description

Full-stack workforce and competence management prototype that combines employee competence, shift requirements, availability and staffing coverage into transparent decision support.

## What this project demonstrates

### Software engineering

- C# and ASP.NET Core/.NET 10
- React 19 and Vite
- REST-style API design
- Entity Framework Core
- SQL Server
- Docker and Docker Compose
- GitHub Actions CI/CD

### Application design

- domain-oriented service separation
- competence and staffing data modelling
- validation at API boundaries
- explicit hard failures versus reviewable warnings
- explainable decision-support results
- non-destructive what-if analysis
- auditability

### Security engineering

- HTTP-only authentication cookies
- JWT validation
- role-aware mutation controls
- authentication rate limiting
- account lockout
- CORS allow-listing
- security response headers
- CodeQL
- Dependabot
- dependency review
- non-root container execution
- bounded file uploads and controlled import paths

### Quality engineering

- 19 backend xUnit tests
- frontend lint/build validation
- EF migration/model validation
- Docker Compose verification
- authenticated end-to-end smoke flow
- documented production boundary

## Why the project is technically interesting

The project is not only CRUD. The application contains a small decision-support domain in which staffing coverage depends on several related factors:

```text
Employee
  |
  +--> Role
  +--> Competence + Level + Validity
  +--> Availability / Absence
  +--> Employment percentage
  |
  v
Shift
  |
  +--> Minimum staffing
  +--> Required competence
  +--> Required level/count
  +--> Required role
  +--> Critical requirement
  |
  v
CoverageService / PlanningAdvisor
  |
  v
Explainable GREEN / YELLOW / RED result
```

The important engineering property is that the software exposes why a result was produced instead of hiding the decision behind a single status value.

## Responsible-use boundary

This is a portfolio prototype and decision-support system. It does not make autonomous employment, staffing or clinical decisions.

The project must not be described as legally compliant merely because it contains working-time checks. The scheduling values are configurable prototype baselines and must be validated against applicable legislation, collective agreements and local policy before any real-world use.

The hosted demonstration must use synthetic data only.

## Suggested LinkedIn project entry

**Workforce & Competence Management — Full-stack decision-support application**

Built a full-stack workforce and competence-management prototype using React, ASP.NET Core/.NET 10, EF Core, SQL Server and Docker. The application models employee competence, availability and shift requirements and provides explainable staffing-coverage and candidate-planning support. Implemented authentication, role-aware access controls, auditability, automated testing, CI/CD, CodeQL, Dependabot and controlled data-exchange workflows.

**Technologies:** C#, .NET 10, ASP.NET Core, React, JavaScript, Vite, EF Core, SQL Server, Docker, GitHub Actions, CodeQL, Dependabot.

## Evidence to show

When presenting the project, the strongest evidence is:

1. live demo
2. architecture diagram
3. staffing coverage screen
4. competence-management screen
5. candidate/what-if planning screen
6. GitHub Actions CI result
7. security documentation
8. test matrix

Avoid presenting only source-code screenshots. Show the relationship between the problem, the system design, the implementation and the verification evidence.
