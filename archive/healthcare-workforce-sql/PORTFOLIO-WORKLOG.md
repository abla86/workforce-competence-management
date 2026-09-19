# Portfolio Worklog

## 2026-08-26 — Validation hardening

- Strengthened `sql/07-validation.sql` with explicit structural and value checks.
- Validation now detects orphaned shift assignments and requirements.
- Validation now checks employee percentage and shift-value ranges before reporting PASS.
- A failed validation now returns failure details and terminates with a SQL error instead of silently continuing.
- Existing schema, views, stored procedure and analysis queries were not changed.

## 2026-08-26 — Baseline CI verification

- Added a minimal GitHub Actions workflow for parser-level T-SQL verification.
- SQLFluff 4.3.0 parses `setup.sql` and the `sql/` directory using the T-SQL dialect.
- README now distinguishes parser validation from execution against SQL Server.

## Status

Portfolio / database-design demonstration using fictional workforce data. Automated parser verification is configured; SQL Server execution remains an environment-level check.
