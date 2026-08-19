# Vaktklar prototype completion

This document defines the prototype boundary for the Vaktklar staffing and competence engine.

## Implemented target

- Shift-task based coverage evaluation
- Competence, role, availability and authorization checks
- Staffing minimums
- Double-booking and minimum-rest checks when shift times are available
- Scenario analysis and qualified replacement suggestions
- Protected audit details and anonymized audit summaries
- Authentication and role-based authorization
- GDPR self-service export and privacy requests
- Automatic audit retention cleanup
- Frontend integration with the real coverage API
- CRUD endpoints required by the existing frontend

## Prototype boundary

The development authentication provider and demo token endpoint are for local prototyping only. Production identity must be supplied by a real OIDC/OAuth2 identity provider, with secrets supplied through the deployment secret store.

GDPR endpoints implement technical request handling for the prototype. Legal basis, retention periods, data controller responsibilities, DPIA and operational procedures must be approved by the responsible organization before production use.
