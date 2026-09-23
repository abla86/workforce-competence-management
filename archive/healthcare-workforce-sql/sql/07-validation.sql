USE HealthWorkforceDb;
GO

SET NOCOUNT ON;

DECLARE @Failures TABLE
(
    TestName NVARCHAR(200) NOT NULL,
    Details  NVARCHAR(4000) NOT NULL
);

DECLARE @Employees INT = (SELECT COUNT(*) FROM dbo.Employees);
DECLARE @Shifts INT = (SELECT COUNT(*) FROM dbo.Shifts);
DECLARE @Assignments INT = (SELECT COUNT(*) FROM dbo.ShiftAssignments);

IF @Employees = 0
    INSERT INTO @Failures VALUES ('Employees', 'Expected at least one employee.');

IF @Shifts = 0
    INSERT INTO @Failures VALUES ('Shifts', 'Expected at least one shift.');

IF EXISTS (
    SELECT 1
    FROM dbo.ShiftAssignments sa
    LEFT JOIN dbo.Employees e ON e.EmployeeId = sa.EmployeeId
    LEFT JOIN dbo.Shifts s ON s.ShiftId = sa.ShiftId
    WHERE e.EmployeeId IS NULL OR s.ShiftId IS NULL
)
    INSERT INTO @Failures VALUES ('Assignment integrity', 'Found assignment rows without a matching employee or shift.');

IF EXISTS (
    SELECT 1
    FROM dbo.ShiftRequirements sr
    LEFT JOIN dbo.Shifts s ON s.ShiftId = sr.ShiftId
    LEFT JOIN dbo.Competencies c ON c.CompetencyId = sr.CompetencyId
    WHERE s.ShiftId IS NULL OR c.CompetencyId IS NULL
)
    INSERT INTO @Failures VALUES ('Requirement integrity', 'Found requirement rows without a matching shift or competency.');

IF EXISTS (
    SELECT 1
    FROM dbo.Employees
    WHERE PositionPercent <= 0 OR PositionPercent > 100
)
    INSERT INTO @Failures VALUES ('Employee validation', 'Found PositionPercent outside the allowed range.');

IF EXISTS (
    SELECT 1
    FROM dbo.Shifts
    WHERE Hours <= 0 OR Hours > 24 OR MinimumStaff <= 0
)
    INSERT INTO @Failures VALUES ('Shift validation', 'Found invalid shift hours or staffing requirement values.');

SELECT 'Employees' AS TestName, @Employees AS Result;
SELECT 'Shifts' AS TestName, @Shifts AS Result;
SELECT 'Assignments' AS TestName, @Assignments AS Result;
SELECT 'Understaffed shifts' AS TestName, COUNT(*) AS Result
FROM dbo.vw_ShiftCoverage
WHERE StaffingStatus = 'UNDERBEMANNET';

SELECT *
FROM dbo.vw_ShiftCoverage
ORDER BY ShiftDate, ShiftType;

IF EXISTS (SELECT 1 FROM @Failures)
BEGIN
    SELECT TestName, Details
    FROM @Failures
    ORDER BY TestName;

    THROW 51000, 'Validation failed. See the returned failure rows for details.', 1;
END;

SELECT CAST('PASS' AS NVARCHAR(10)) AS ValidationStatus,
       'All structural and value validations passed.' AS Details;
GO
