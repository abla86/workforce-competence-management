# Functional verification matrix

| Area | Verification | Current evidence |
|---|---|---|
| Backend | Restore/build/test | GitHub Actions backend job |
| Frontend | npm ci/lint/build | GitHub Actions frontend job |
| Docker | Compose config/build/start | GitHub Actions Docker job |
| SQL Server | Container health + API DB connectivity | Full-stack smoke test |
| Authentication | Bootstrap + login | Full-stack smoke test |
| Protected API | Dashboard + shifts | Full-stack smoke test |
| Coverage | Unit tests and runtime API | Backend tests + smoke test |
| Candidate planning | Unit tests | Backend test suite |
| Absence scenarios | Unit tests/runtime endpoint | Backend code and scenario endpoint |
| CodeQL | Analysis | Successful CodeQL workflow |
| Dependency security | Dependabot configuration | Repository configuration |

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

## Interpretation

A passing CI run proves the tested build and smoke paths work. It does not prove regulatory compliance, absence of all security vulnerabilities, or production readiness.
