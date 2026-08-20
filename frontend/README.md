# Workforce Web UI

React/Vite frontend for the Workforce & Competence Management prototype.

## Main views

- **Dashboard** — staffing status, competence coverage and action-required shifts
- **Employees** — employee administration and competence records
- **Competence** — competence catalogue and workforce coverage
- **Shifts** — shift planning, staffing assignments, requirements and live coverage
- **Gap Analysis** — staffing and competence gaps

## Technology

- React 19
- Vite 7
- JavaScript/JSX
- ESLint
- REST API integration with the ASP.NET Core backend

## Run locally

```bash
npm ci
npm run dev
```

The default development API configuration is defined in `src/services/api.js`. When using Docker Compose, the frontend is served on `http://localhost:8088` and the API on `http://localhost:5080`.

## Validate

```bash
npm run lint
npm run build
```

## Functional workflow

The primary planning workflow is:

```text
Employee
   ↓
Competence + validity
   ↓
Shift plan
   ↓
Staffing + competence requirements
   ↓
Coverage evaluation
   ↓
GREEN / YELLOW / RED
   ↓
Candidate ranking / what-if analysis
```

The frontend presents backend decisions and explanations; it does not independently replace the backend validation rules.
