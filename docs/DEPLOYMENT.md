# Deployment Guide

## Scope

This repository is a runnable full-stack prototype. The deployment path below is intended for controlled demo/internal environments. Do not expose the demo configuration directly to the public internet.

## Architecture

```text
Browser
  ↓ HTTPS
React frontend
  ↓ HTTPS
ASP.NET Core API (.NET 10)
  ↓
SQL Server
```

## Database migrations

The repository contains a checked-in initial EF Core migration and model snapshot. The API calls `Database.MigrateAsync()` before seeding, so a fresh SQL Server database is created from migration history rather than `EnsureCreated()`.

For a clean local verification:

```bash
docker compose down -v
docker compose up --build
curl --fail http://localhost:5080/health
```

For future model changes:

```bash
dotnet ef migrations add <DescriptiveName> --project backend/Workforce.Api --startup-project backend/Workforce.Api
```

Review the generated migration and snapshot, run the full test suite, then deploy the application so pending migrations are applied in the controlled target environment.

## Required production controls

Before public/organizational deployment:

- TLS/HTTPS at the edge
- `SECURITY_COOKIE_SECURE=true`
- production secrets supplied by the hosting platform, never committed to Git
- persistent ASP.NET Core Data Protection keys
- controlled migration deployment and rollback procedure
- managed SQL Server or protected SQL Server instance with backups
- backup/restore verification
- managed database least-privilege account
- OIDC/Entra ID and MFA where required by the target organization
- granular RBAC
- audit actor attribution
- logging/monitoring/alerting
- privacy/GDPR assessment
- vulnerability and penetration testing appropriate to the deployment

## Container deployment

The repository includes Docker Compose for the complete local stack. For a hosted environment, use the same images with a managed database and platform-managed secrets where possible.

```bash
docker compose config --quiet
docker compose build
docker compose up -d
curl --fail http://localhost:5080/health
```

## Demo configuration

The CI workflow uses isolated CI-only credentials. These values are not production credentials and must never be reused in a public deployment.

## Release checklist

- [ ] Build passes
- [ ] Backend tests pass
- [ ] Frontend lint/build passes
- [ ] Docker Compose validates
- [ ] SQL Server health passes
- [ ] API health passes
- [ ] Fresh database migration passes
- [ ] Authentication smoke test passes
- [ ] Protected endpoint smoke test passes
- [ ] CodeQL passes
- [ ] Dependabot reviewed
- [ ] Production secrets configured outside source control
- [ ] TLS enabled
- [ ] Backup/restore verified
- [ ] Monitoring enabled
- [ ] Privacy/security review completed
