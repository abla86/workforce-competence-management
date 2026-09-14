# Data formats and exchange strategy

## Supported application representations

### JSON

Primary machine-readable format for API requests/responses, scenario results and backup-oriented data exchange.

### CSV

Recommended tabular format for employee, competence and planning datasets where spreadsheet interoperability is required.

### HTML

Suitable for human-readable shared planning views and browser-based review.

### SQL Server

Authoritative runtime persistence for the application database.

## Recommended interchange rules

- Preserve stable integer identifiers when importing related records.
- Use ISO-8601 dates (`YYYY-MM-DD`) and ISO time representations in API payloads.
- Validate imported values before persistence.
- Never import credentials, JWT secrets or bootstrap keys as business data.
- Treat exports containing employee data as confidential operational information.
- Do not use an exported report as a substitute for the live database when current status matters.

## Minimum data domains

A complete workforce plan consists of:

- employees
- employee competences
- competence catalogue
- shifts
- shift assignments
- shift competence requirements
- absences

## Data integrity

The backend rejects invalid position percentages, invalid shift duration/staffing values, invalid absence ranges and duplicate competence records. Historical shift assignments protect employee deletion.

## Security classification

Employee names, roles, competence status, absence information and staffing plans should be treated as sensitive operational information. Exports must therefore be protected by the same organisational access controls as the application.
