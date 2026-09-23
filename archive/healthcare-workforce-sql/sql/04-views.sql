USE HealthWorkforceDb;
GO

CREATE OR ALTER VIEW dbo.vw_ShiftCoverage
AS
SELECT
    s.ShiftId,
    s.ShiftDate,
    s.ShiftType,
    s.MinimumStaff,

    COUNT(sa.EmployeeId) AS AssignedStaff,

    CASE
        WHEN COUNT(sa.EmployeeId) >= s.MinimumStaff
        THEN 'OK'
        ELSE 'UNDERBEMANNET'
    END AS StaffingStatus,

    s.MinimumStaff - COUNT(sa.EmployeeId)
        AS StaffingDifference

FROM dbo.Shifts s

LEFT JOIN dbo.ShiftAssignments sa
    ON sa.ShiftId = s.ShiftId

GROUP BY
    s.ShiftId,
    s.ShiftDate,
    s.ShiftType,
    s.MinimumStaff;
GO


CREATE OR ALTER VIEW dbo.vw_EmployeeHours
AS
SELECT
    e.EmployeeId,
    e.EmployeeName,
    e.RoleName,
    e.PositionPercent,

    COALESCE(SUM(s.Hours), 0)
        AS PlannedHours

FROM dbo.Employees e

LEFT JOIN dbo.ShiftAssignments sa
    ON sa.EmployeeId = e.EmployeeId

LEFT JOIN dbo.Shifts s
    ON s.ShiftId = sa.ShiftId

GROUP BY
    e.EmployeeId,
    e.EmployeeName,
    e.RoleName,
    e.PositionPercent;
GO
