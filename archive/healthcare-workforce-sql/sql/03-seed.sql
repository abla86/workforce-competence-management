USE HealthWorkforceDb;
GO

SET NOCOUNT ON;

INSERT INTO dbo.Employees (EmployeeName, RoleName, PositionPercent, IsActive)
VALUES
    (N'Anna Hansen', N'Sykepleier', 100.00, 1),
    (N'Bjørn Olsen', N'Helsefagarbeider', 80.00, 1),
    (N'Clara Nilsen', N'Sykepleier', 75.00, 1),
    (N'Dag Berg', N'Helsefagarbeider', 100.00, 1);
GO

INSERT INTO dbo.Competencies (CompetencyName)
VALUES
    (N'Sykepleier'),
    (N'Medikamenthåndtering'),
    (N'Demens'),
    (N'Akuttkompetanse');
GO

INSERT INTO dbo.EmployeeCompetencies (EmployeeId, CompetencyId)
SELECT e.EmployeeId, c.CompetencyId
FROM (VALUES
    (N'Anna Hansen', N'Sykepleier'),
    (N'Anna Hansen', N'Medikamenthåndtering'),
    (N'Anna Hansen', N'Akuttkompetanse'),
    (N'Bjørn Olsen', N'Demens'),
    (N'Clara Nilsen', N'Sykepleier'),
    (N'Clara Nilsen', N'Medikamenthåndtering'),
    (N'Dag Berg', N'Demens')
) AS x(EmployeeName, CompetencyName)
JOIN dbo.Employees e ON e.EmployeeName = x.EmployeeName
JOIN dbo.Competencies c ON c.CompetencyName = x.CompetencyName;
GO

INSERT INTO dbo.Shifts (ShiftDate, ShiftType, Hours, MinimumStaff)
VALUES
    ('2026-09-07', N'Dag', 7.50, 2),
    ('2026-09-07', N'Kveld', 7.50, 2),
    ('2026-09-08', N'Natt', 10.00, 1);
GO

INSERT INTO dbo.ShiftRequirements (ShiftId, CompetencyId, MinimumCount)
SELECT s.ShiftId, c.CompetencyId, x.MinimumCount
FROM (VALUES
    ('2026-09-07', N'Dag', N'Sykepleier', 1),
    ('2026-09-07', N'Kveld', N'Demens', 1),
    ('2026-09-08', N'Natt', N'Sykepleier', 1)
) AS x(ShiftDate, ShiftType, CompetencyName, MinimumCount)
JOIN dbo.Shifts s
    ON s.ShiftDate = x.ShiftDate
   AND s.ShiftType = x.ShiftType
JOIN dbo.Competencies c
    ON c.CompetencyName = x.CompetencyName;
GO

INSERT INTO dbo.ShiftAssignments (ShiftId, EmployeeId)
SELECT s.ShiftId, e.EmployeeId
FROM (VALUES
    ('2026-09-07', N'Dag', N'Anna Hansen'),
    ('2026-09-07', N'Dag', N'Bjørn Olsen'),
    ('2026-09-07', N'Kveld', N'Clara Nilsen'),
    ('2026-09-08', N'Natt', N'Anna Hansen')
) AS x(ShiftDate, ShiftType, EmployeeName)
JOIN dbo.Shifts s
    ON s.ShiftDate = x.ShiftDate
   AND s.ShiftType = x.ShiftType
JOIN dbo.Employees e
    ON e.EmployeeName = x.EmployeeName;
GO
