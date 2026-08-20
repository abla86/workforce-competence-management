# Workforce & Competence Management — security and first-run setup

## Production secrets

Set these values outside Git:

- `DB_PASSWORD`
- `JWT_SECRET_KEY` — at least 32 UTF-8 bytes; use a cryptographically random value
- `VAKTKLAR_BOOTSTRAP_KEY` — one-time administrator bootstrap secret

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

- Unauthenticated access: health endpoint and authentication/bootstrap endpoints only.
- Read access: authenticated users.
- Planning-data write access: Admin, Manager and HR roles.
- Login is rate-limited and accounts are temporarily locked after repeated failed attempts.
- Authentication uses an HTTP-only JWT cookie rather than browser local storage.

## Data exchange

The current prototype includes authenticated employee/competence import and export plus shift-plan export/share functions. These endpoints expose workforce-related information and must therefore be treated as protected application data.

Before production use, the data-exchange layer should enforce the organization's final role model explicitly for import/export/share operations, provide centralized audit identity for every exchange operation, and be covered by integration tests for authorization boundaries.

## Important production work remaining

Authentication is a functional application security layer, but production deployment should still add an established identity provider/OIDC flow, centralized audit identity, MFA, CSRF protection appropriate to the final cookie architecture, secret rotation, external logging/monitoring, database migration management, backup/restore testing and a formal GDPR/security review before handling real employee data.
