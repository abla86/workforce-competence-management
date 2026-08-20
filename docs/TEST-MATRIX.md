# Functional verification matrix

| Area | Verification | Current evidence |
|---|---|---|
| Backend | Restore/build/test | GitHub Actions backend job |
| Frontend | npm ci/lint/build | GitHub Actions frontend job |
| Docker | Compose config/build/start | GitHub Actions Docker job |
| SQL Server | Container health + API DB connectivity | Full-stack smoke test |
| Authentication | Bootstrap + login | Full-stack smoke test |
| Authorization | Unauthenticated API returns 401 | Full-stack smoke test |
| Employee | Create/update/delete rules | API regression smoke test + backend tests |
| Competence | Create + employee competence assignment | API regression smoke test |
| Shift | Create + assignment + requirement | API regression smoke test |
| Coverage | Runtime coverage evaluation | API regression smoke test + backend tests |
| Candidate planning | Candidate endpoint and planning rules | API regression smoke test + backend tests |
| What-if | Scenario does not persist assignment changes | Backend tests + runtime endpoint |
| Absence scenarios | Scenario endpoint and planning rules | Backend tests/runtime endpoint |
| Audit | Audit endpoint after operational changes | API regression smoke test |
| Data exchange | JSON/CSV/HTML/ICS/PDF browser workflows | Frontend build + manual functional verification |
| Import validation | File size, record limits, schema/value validation | Frontend validation logic |
| CodeQL | Static analysis | CodeQL workflow |
| Dependency security | Dependabot configuration | Repository configuration |

## Automated end-to-end regression flow

The Docker CI job now executes the core operational path after authentication:

1. Start SQL Server, API and frontend.
2. Verify API health and frontend availability.
3. Verify an unauthenticated protected API returns `401`.
4. Bootstrap and authenticate a CI-only administrator.
5. Create an employee.
6. Create a competence.
7. Assign the competence to the employee.
8. Create a shift.
9. Add a critical competence requirement to the shift.
10. Assign the qualified employee to the shift.
11. Evaluate coverage.
12. Retrieve candidate rankings.
13. Retrieve audit events.
14. Run a what-if coverage scenario.
15. Verify the frontend remains reachable.
16. Tear down the complete stack.

The CI data is synthetic and disposable. It must never contain real employee or patient information.

## Required regression scenarios

1. Employee cannot be assigned when a hard competence requirement fails.
2. Employee cannot be assigned during conflicting absence.
3. Employee cannot be double-booked in overlapping shifts.
4. Rest-period violations are surfaced as hard failures/warnings according to planning rules.
5. Minimum staffing deficits produce a non-GREEN result.
6. Critical competence deficits produce RED.
7. Non-critical warnings can produce YELLOW.
8. What-if analysis does not mutate persisted assignments.
9. Historical assignments prevent destructive employee deletion.
10. Unauthenticated API access returns `401`.
11. Authentication rate limiting returns `429` when limits are exceeded.
12. Docker startup waits for SQL Server and API health before frontend smoke checks.
13. Malformed/oversized JSON import is rejected before records are submitted.
14. Exported HTML escapes operational values before writing them into markup.
15. ICS export preserves local shift wall-clock times, including shifts crossing midnight.

## Interpretation

A passing CI run proves the tested build and smoke paths work. It does not prove regulatory compliance, absence of all security vulnerabilities, or production readiness. Browser-specific export behavior, accessibility and responsive behavior still require a real browser/device QA pass.
