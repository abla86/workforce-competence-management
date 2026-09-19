USE HealthWorkforceDb;
GO

PRINT '=== ALL EMPLOYEES ===';

SELECT
    EmployeeId,
    EmployeeName,
    RoleName,
    PositionPercent,
    IsActive
FROM dbo.Employees
ORDER BY EmployeeName;
GO


PRINT '=== SHIFT COVERAGE ===';

SELECT *
FROM dbo.vw_ShiftCoverage
ORDER BY ShiftDate, ShiftType;
GO


PRINT '=== UNDERSTAFFED SHIFTS ===';

SELECT
    ShiftDate,
    ShiftType,
    MinimumStaff,
    AssignedStaff,
    ABS(StaffingDifference) AS MissingStaff
FROM dbo.vw_ShiftCoverage
WHERE StaffingStatus = 'UNDERBEMANNET'
ORDER BY ShiftDate;
GO


PRINT '=== EMPLOYEE HOURS ===';

SELECT *
FROM dbo.vw_EmployeeHours
ORDER BY PlannedHours DESC;
GO


PRINT '=== EMPLOYEES WITH COMPETENCIES ===';

SELECT
    e.EmployeeName,
    e.RoleName,
    c.CompetencyName

FROM dbo.Employees e

JOIN dbo.EmployeeCompetencies ec
    ON ec.EmployeeId = e.EmployeeId

JOIN dbo.Competencies c
    ON c.CompetencyId = ec.CompetencyId

ORDER BY
    e.EmployeeName,
    c.CompetencyName;
GO


PRINT '=== NURSES ===';

SELECT DISTINCT
    e.EmployeeId,
    e.EmployeeName,
    e.PositionPercent

FROM dbo.Employees e

JOIN dbo.EmployeeCompetencies ec
    ON ec.EmployeeId = e.EmployeeId

JOIN dbo.Competencies c
    ON c.CompetencyId = ec.CompetencyId

WHERE c.CompetencyName = 'Sykepleier';
GO


PRINT '=== SHIFT COMPETENCE COVERAGE ===';

SELECT
    s.ShiftDate,
    s.ShiftType,
    c.CompetencyName AS RequiredCompetency,
    sr.MinimumCount,

    COUNT(DISTINCT ec.EmployeeId)
        AS AssignedWithCompetency,

    CASE
        WHEN COUNT(DISTINCT ec.EmployeeId)
             >= sr.MinimumCount
        THEN 'OK'
        ELSE 'MANGLER KOMPETANSE'
    END AS CompetencyStatus

FROM dbo.Shifts s

JOIN dbo.ShiftRequirements sr
    ON sr.ShiftId = s.ShiftId

JOIN dbo.Competencies c
    ON c.CompetencyId = sr.CompetencyId

LEFT JOIN dbo.ShiftAssignments sa
    ON sa.ShiftId = s.ShiftId

LEFT JOIN dbo.EmployeeCompetencies ec
    ON ec.EmployeeId = sa.EmployeeId
    AND ec.CompetencyId = sr.CompetencyId

GROUP BY
    s.ShiftDate,
    s.ShiftType,
    c.CompetencyName,
    sr.MinimumCount

ORDER BY
    s.ShiftDate,
    s.ShiftType,
    c.CompetencyName;
GO


PRINT '=== FULL SHIFT OVERVIEW ===';

SELECT
    s.ShiftDate,
    s.ShiftType,
    e.EmployeeName,
    e.RoleName,
    s.Hours

FROM dbo.Shifts s

LEFT JOIN dbo.ShiftAssignments sa
    ON sa.ShiftId = s.ShiftId

LEFT JOIN dbo.Employees e
    ON e.EmployeeId = sa.EmployeeId

ORDER BY
    s.ShiftDate,
    s.ShiftType,
    e.EmployeeName;
GO
