# Workforce & Competence Management System

Full-stack application for workforce planning, competence management, staffing coverage and gap analysis.

The system is industry-neutral and demonstrates how employee competence, staffing requirements and operational planning can be combined in one application.

## Core capabilities

- Employee overview and workforce profiles
- Competence matrix with proficiency levels
- Competence validity and review status
- Shift planning
- Minimum staffing requirements
- Competence requirements per shift
- Automatic staffing gap analysis
- Automatic competence gap analysis
- Coverage percentage calculations
- Clear GREEN / AMBER / RED status indicators
- Status text in addition to color for accessibility
- Dashboard for workforce and competence overview
- Search and filtering

## Technology stack

### Frontend

- React
- Vite
- JavaScript
- Responsive CSS

### Backend

- C#
- .NET 9
- ASP.NET Core
- REST API
- Entity Framework Core

### Data

- SQL Server
- Relational data model
- Employee / competence many-to-many relationships
- Shift assignments
- Shift competence requirements

### Engineering

- xUnit
- Docker
- Docker Compose
- GitHub Actions
- CodeQL
- Dependabot
- Git
- GitHub

## Architecture

    React frontend
          |
          v
    ASP.NET Core REST API
          |
          v
    Entity Framework Core
          |
          v
       SQL Server

The complete stack can run locally using Docker Compose.

## Dashboard

The dashboard summarizes:

- active employees
- tracked competences
- overall competence coverage
- shifts requiring action
- staffing status
- competence status

## Status model

The application does not rely on color alone.

- GREEN: GOOD / COVERED / ACTIVE
- AMBER: ATTENTION / REVIEW DUE
- RED: ACTION REQUIRED / MISSING / UNDERSTAFFED

This makes operational gaps easy to identify while preserving accessibility.

## Gap analysis

For each shift, the application evaluates both:

1. whether minimum staffing requirements are met
2. whether required competence is available at the required proficiency level

Example:

    Evening shift
    Staffing: 3 / 4       UNDERSTAFFED
    First aid: 2 / 1      COVERED
    Advanced assessment: 0 / 1   MISSING

The result is summarized as either GOOD or ACTION REQUIRED.

## Automated verification

Backend coverage logic is tested with xUnit.

Verified locally:

- API build succeeds
- 3/3 backend tests pass
- frontend lint passes
- frontend production build passes
- SQL Server runs successfully in Docker
- ASP.NET Core API connects to SQL Server through Entity Framework Core
- React frontend runs successfully through Docker Compose

## Run with Docker Compose

From the repository root:

    docker compose up --build

Frontend:

    http://localhost:8088

API:

    http://localhost:5080

Health endpoint:

    http://localhost:5080/health

Stop the stack:

    docker compose down

## Local development

### Backend

    cd backend\Workforce.Api
    dotnet restore
    dotnet run

### Backend tests

    dotnet test backend\Workforce.Api.Tests\Workforce.Api.Tests.csproj --configuration Release

### Frontend

    cd frontend
    npm install
    npm run dev

### Frontend verification

    npm run lint
    npm run build

## Demo data

All employee and competence data in this repository is fictional demonstration data.

No real employee, patient or sensitive personal information is included.

## Purpose

This project demonstrates full-stack development using a practical workforce-management problem rather than a single technology exercise.

It combines frontend development, backend APIs, relational data, business rules, automated testing, containerization and development automation in one system.

## Author

Anne Beth Andersen

## Portfolio

This project is the current featured full-stack project in my developer portfolio.

The portfolio also presents the complete development progression, including JavaScript applications, React, Python, C#, REST APIs, SQL, Docker and CI/security workflows.

Portfolio:
https://abla86.github.io/developer-portfolio/
