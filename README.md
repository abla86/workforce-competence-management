# Workforce & Competence Management System

A full-stack workforce planning and competence management application.

The system is deliberately industry-neutral. It can model staffing and competence requirements in healthcare, municipalities, logistics, industry, IT, HR and other workforce-based environments.

## What it demonstrates

### Frontend
- React + Vite
- Responsive dashboard
- Employees
- Competence matrix
- Shift planning
- Gap analysis
- Search and filtering
- Accessible status design using both color and text

### Backend
- ASP.NET Core Minimal API
- Entity Framework Core
- SQL Server
- REST endpoints
- Business-rule validation
- Workforce coverage calculations
- Competence gap analysis

### Engineering
- xUnit tests
- Docker
- Docker Compose
- GitHub Actions CI
- CodeQL
- Dependabot

## Status system

The UI never relies on color alone:

- GREEN — COVERED / ACTIVE / GOOD
- AMBER — REVIEW / PARTIAL / ATTENTION
- RED — MISSING / UNDERSTAFFED / ACTION REQUIRED

## Project structure

```text
workforce-competence-management/
├── backend/
│   ├── Workforce.Api/
│   └── Workforce.Api.Tests/
├── frontend/
├── docker-compose.yml
└── .github/
```

## Local development

### Backend

The default development connection uses SQL Server LocalDB:

```powershell
cd backend\Workforce.Api
dotnet restore
dotnet run
```

The API seeds demo data automatically on first run.

### Frontend

```powershell
cd frontend
npm install
npm run dev
```

The frontend expects the API at `http://localhost:5080`.

## Docker Compose

Docker Compose runs:

- SQL Server
- ASP.NET Core API
- React/nginx frontend

```powershell
docker compose up --build
```

Frontend:

```text
http://localhost:8088
```

API:

```text
http://localhost:5080
```

## Demo data

All names and workforce data are fictional.

Do not use real personal or sensitive employee information without applying your organization's information-security, privacy, access-control and retention requirements.
