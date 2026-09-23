USE HealthWorkforceDb;
GO

INSERT INTO dbo.Employees
    (EmployeeName, RoleName, PositionPercent)
VALUES
    ('Anne', 'Sykepleier', 100),
    ('Kari', 'Helsefagarbeider', 80),
    ('Per', 'Helsefagarbeider', 100),
    ('Liv', 'Sykepleier', 60),
    ('Ola', 'Pleiemedarbeider', 50);
GO

INSERT INTO dbo.Competencies
    (CompetencyName)
VALUES
    ('Sykepleier'),
    ('Legemiddelhåndtering'),
    ('Palliasjon'),
    ('Helsefagarbeider');
GO

INSERT INTO dbo.EmployeeCompetencies
    (EmployeeId, CompetencyId)
VALUES
    (1,1),
    (1,2),
    (1,3),
    (2,2),
    (2,4),
    (3,4),
    (4,1),
    (4,2);
GO

INSERT INTO dbo.Shifts
    (ShiftDate, ShiftType, Hours, MinimumStaff)
VALUES
    ('2026-08-14', 'Dag',   7.5, 3),
    ('2026-08-14', 'Kveld', 7.0, 3),
    ('2026-08-14', 'Natt',  10.0, 2),
    ('2026-08-15', 'Dag',   7.5, 3);
GO

INSERT INTO dbo.ShiftRequirements
    (ShiftId, CompetencyId, MinimumCount)
VALUES
    (1,1,1),
    (1,2,1),
    (2,1,1),
    (3,1,1),
    (4,1,1),
    (4,2,1);
GO

INSERT INTO dbo.ShiftAssignments
    (ShiftId, EmployeeId)
VALUES
    (1,1),
    (1,2),
    (1,3),

    (2,2),
    (2,3),

    (3,4),

    (4,1),
    (4,2),
    (4,5);
GO
