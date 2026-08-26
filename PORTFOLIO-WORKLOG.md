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

## Scope

This change improves resilience of the existing frontend ↔ API connection. It does not claim automatic repair of application defects, data corruption or infrastructure failure.
