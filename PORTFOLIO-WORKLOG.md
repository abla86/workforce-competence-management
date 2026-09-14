# Portfolio Worklog

## 2026-08-26 — Resilient application communication

- Hardened the shared frontend API client used by the application pages.
- Added a bounded 15-second request timeout.
- Added automatic recovery only for idempotent read requests (`GET`, `HEAD`, `OPTIONS`) when transient network or gateway/service-limit failures occur.
- Deliberately excluded state-changing requests from automatic retries to avoid duplicate employee, competence, shift, assignment or import writes.
- Each retry now receives a fresh `AbortController` and timeout, preventing a timed-out attempt from poisoning later retry attempts.
- Preserved structured API errors and request-id capture when the backend provides `X-Request-ID`.
- Applied the same recovery layer to authenticated downloads.
- Updated the README architecture and employer-facing evidence to reflect the actual integration and safety boundary.

## 2026-08-26 — Shared scheduling safety policy

- Centralised scheduling safety baselines in `SchedulingRules.cs` so candidate ranking and coverage evaluation use the same policy source.
- Exposed explicit baseline values for daily rest, weekly rest, default weekly hours and maximum shift duration.
- Updated coverage rest warnings to use the shared daily-rest constant instead of a separate hard-coded value.
- Added a regression test confirming the shared scheduling policy values are available to the coverage test suite.
- Kept the policy explicitly documented as a configurable safety baseline rather than claiming it replaces applicable agreements, local rules or legal assessment.

## Scope

The changes strengthen the existing frontend/API and scheduling-rule integration. They do not claim automatic repair of application defects, data corruption, infrastructure failure or legal compliance. The latest GitHub Actions verification remains the source of truth for build/test status.
