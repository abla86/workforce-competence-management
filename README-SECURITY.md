# Workforce & Competence Management — security and first-run setup

## Production secrets

Set these values outside Git:

- `DB_PASSWORD`
- `JWT_SECRET_KEY` — at least 32 UTF-8 bytes; use a cryptographically random value
- `VAKTKLAR_BOOTSTRAP_KEY` — one-time administrator bootstrap secret
- `SECURITY_COOKIE_SECURE=true` in HTTPS production deployments

The real `.env` file is ignored by Git. Do not put production secrets in `appsettings.json` or source code.

## First administrator

1. Start SQL Server and the API.
2. Call `POST /api/auth/bootstrap` once with:

```json
{
  "bootstrapKey": "<VAKTKLAR_BOOTSTRAP_KEY>",
  "username": "admin",
  "password": "<a unique password of at least 12 characters>"
}
```

3. Delete or rotate the bootstrap secret after successful setup.
4. Log in through the web interface.

Bootstrap is refused once any user account exists.

## Access model

- Unauthenticated access: health, root API metadata, OpenAPI and authentication/bootstrap endpoints.
- Read access: authenticated users in the current prototype.
- Planning-data write access: Admin, Manager and HR roles.
- Login and bootstrap are rate-limited; accounts are temporarily locked after repeated failed login attempts.
- Authentication uses an HTTP-only JWT cookie rather than browser local storage.

The final production role model should explicitly define which workforce, absence, audit and export data each role may read.

## Browser and proxy security

- Local Docker development uses HTTP and therefore `SECURITY_COOKIE_SECURE=false`.
- Production must terminate TLS and use `SECURITY_COOKIE_SECURE=true`.
- The Nginx frontend adds `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` and `Permissions-Policy` response headers.
- Nginx forwards the original host and proxy information to the API.
- Production deployments should apply HTTPS/HSTS at the TLS-terminating edge and restrict trusted reverse proxies appropriately.

## Data exchange

The current prototype includes authenticated employee/competence import and export plus shift-plan export/share functions. These endpoints expose workforce-related information and must therefore be treated as protected application data.

Before production use, the data-exchange layer should enforce the organization's final role model explicitly for import/export/share operations, provide centralized audit identity for every exchange operation, and be covered by integration tests for authorization boundaries.

## Audit and logging

The prototype stores application audit events in SQL Server for mutations and coverage evaluations. The current helper still uses a system actor for many mutation events, so the audit trail should **not** yet be presented as a complete user-attribution control.

Production work should add authenticated actor attribution, restricted audit-log access, centralized log collection, monitoring/alerting and tamper-evident or immutable retention controls.

## Important production work remaining

Authentication is a functional application security layer, but production deployment should still add an established identity provider/OIDC flow, centralized actor attribution, MFA where appropriate, CSRF protection appropriate to the final cookie architecture, secret rotation, external logging/monitoring, EF Core migration management, backup/restore testing and a formal GDPR/security review before handling real employee data.
