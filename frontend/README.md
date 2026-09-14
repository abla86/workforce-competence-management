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

The application uses same-origin `/api` requests by default. When using Docker Compose, the frontend is served on `http://localhost:8088` and Nginx proxies API requests to the backend on `http://localhost:5080`.

For a separate local frontend/API setup, set `VITE_API_URL` to the API origin before starting Vite.

## Validate

```bash
npm run lint
npm run build
```

The repository currently uses lint/build validation rather than a separate automated component or end-to-end frontend test suite.

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
