# Healthcare Workforce SQL — Data Layer for Workforce Domain

A Microsoft SQL Server project modelling workforce, shift and competence planning with fictional demonstration data.

## Portfolio role

**Supporting data-layer project.** This repository complements the active **Workforce & Competence Management** platform. It is kept separate because it demonstrates a distinct technical capability: relational database design and T-SQL analysis. It is not presented as a second workforce application.

## Demonstrated technical scope

- relational data modelling
- employee, role, shift and competence relationships
- many-to-many modelling
- foreign keys, `CHECK`, `UNIQUE` constraints and indexes
- SQL views
- stored procedure
- JOIN, aggregation and filtering queries
- staffing-gap analysis
- competence-coverage analysis
- employee-hour calculations
- validation queries
- parser-level CI validation with SQLFluff

## Database

Microsoft SQL Server. The checked-in scripts define the demonstration schema and analysis workload.

## Verification boundary

Baseline CI parses the repository SQL as T-SQL with SQLFluff. Parser-level validation does not by itself prove execution against a SQL Server instance.

## Data and safety

All workforce names and records are fictional demonstration data. No patient data is included. This is a database-design and analysis project, not a production workforce-management or clinical decision-support system.

## Portfolio

https://abla86.github.io/developer-portfolio/

## Change-control audit

Use repository history and CI results as the source of implementation and verification evidence.
