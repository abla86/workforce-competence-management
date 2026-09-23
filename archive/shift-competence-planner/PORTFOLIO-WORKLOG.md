# Portfolio Worklog

## 2026-08-26 — Baseline improvement

- Added GitHub Actions CI for the existing .NET 9 project.
- CI restores, builds and runs the existing automated tests on pushes and pull requests.
- No application functionality was changed.

## 2026-08-26 — Runtime integration repair

- Fixed the connection between the repository's seed data and the executable application output.
- The existing `data/planner-data.json` is now copied into the application output directory during build.
- The application loads the packaged data file directly instead of relying on a source-tree relative fallback path.
- This keeps the existing models, planner services, save/load flow, CSV export and tests working as one application instead of depending on the development folder layout.

## Status

The repository remains a C#/.NET planning demonstration using fictional workforce data. The README remains the source of truth for its implemented scope.
