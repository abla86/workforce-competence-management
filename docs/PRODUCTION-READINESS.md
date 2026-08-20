# Production readiness boundary

## Current state

The repository is a complete, runnable full-stack prototype suitable for local development, demonstrations, controlled internal testing and portfolio presentation.

## Before production use

The following controls should be completed and validated in the target organisation:

- enterprise identity provider/OIDC and MFA
- HTTPS/TLS termination
- production secret management
- persistent ASP.NET Data Protection keys
- EF Core migrations and controlled schema deployment
- tested backup and restore procedure
- least-privilege database account
- production database encryption and access controls
- granular RBAC for employee/manager/HR/auditor views
- user-attributed audit events
- privacy/GDPR assessment and retention policy
- security review and penetration testing
- centralised logging and alerting
- monitoring and availability objectives
- load/performance testing
- disaster recovery plan

## Do not claim

The project must not be presented as a clinically validated, legally compliant or production-secure workforce system solely because CI passes. CI demonstrates implementation quality for the covered paths; organisational and regulatory controls require separate validation.

## Local/demo start

1. Copy `.env.example` to `.env`.
2. Set non-production values for `DB_PASSWORD`, `JWT_SECRET_KEY` and `VAKTKLAR_BOOTSTRAP_KEY`.
3. Run `docker compose up --build`.
4. Open the frontend on port `8088`.
5. Use the documented bootstrap/login flow.

Never commit `.env` or real credentials.
