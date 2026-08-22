# Data & Migration 2.0

The application now has a dedicated migration workflow for moving workforce data from another system.

## Supported import formats

- CSV
- Excel `.xlsx` / `.xlsm`
- JSON backup
- ICS calendar files

The browser detects the format, normalises common field names and shows a preview before writing data.

## Import workflow

1. Detect format
2. Read fields/columns
3. Suggest mapping from common names (`Name`, `EmployeeName`, `Role`, `Position`, `Department`, `PositionPercent`, `CompetenceName`, `Date`, `StartTime`, `MinimumStaff`)
4. Preview records
5. Validate required fields and ranges
6. Detect conflicts against existing employees, competences and shifts
7. Select conflict behaviour: skip, update or do not create conflicting records
8. Confirm
9. Send one import batch to the API
10. Persist the batch in one EF Core database transaction
11. Record a migration audit event

If the transaction fails, the migration is rolled back rather than leaving a partially imported batch.

## Export formats

- Complete JSON backup
- Complete Excel workbook with Employees, Competences and Shifts sheets
- Employee CSV
- Employee + competence CSV
- Shift-plan Excel-compatible export
- ICS shift calendar
- HTML shift-plan report
- Browser Print / Save as PDF

## Excel implementation

The backend uses `DocumentFormat.OpenXml` for server-side Excel inspection. Version 3.5.1 supports `net10.0` and is MIT licensed. The browser uses the SheetJS standalone build only for workbook parsing/export in the migration workspace; the dependency is loaded when Excel functionality is used.

## Data safety

The migration interface is intended for controlled workforce data. Do not upload real employee or health-related data to an uncontrolled demonstration environment. Production use requires the organisation's privacy, access-control, backup and security controls.
