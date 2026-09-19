USE HealthWorkforceDb;
GO

DROP VIEW IF EXISTS dbo.vw_EmployeeHours;
DROP VIEW IF EXISTS dbo.vw_ShiftCoverage;
GO

DROP TABLE IF EXISTS dbo.ShiftAssignments;
DROP TABLE IF EXISTS dbo.ShiftRequirements;
DROP TABLE IF EXISTS dbo.EmployeeCompetencies;
DROP TABLE IF EXISTS dbo.Shifts;
DROP TABLE IF EXISTS dbo.Competencies;
DROP TABLE IF EXISTS dbo.Employees;
GO

CREATE TABLE dbo.Employees
(
    EmployeeId        INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeName      NVARCHAR(100) NOT NULL,
    RoleName          NVARCHAR(100) NOT NULL,
    PositionPercent   DECIMAL(5,2) NOT NULL,
    IsActive          BIT NOT NULL
        CONSTRAINT DF_Employees_IsActive DEFAULT 1,

    CONSTRAINT CK_Employees_PositionPercent
        CHECK (PositionPercent > 0 AND PositionPercent <= 100)
);
GO

CREATE TABLE dbo.Competencies
(
    CompetencyId      INT IDENTITY(1,1) PRIMARY KEY,
    CompetencyName    NVARCHAR(100) NOT NULL UNIQUE
);
GO

CREATE TABLE dbo.EmployeeCompetencies
(
    EmployeeId        INT NOT NULL,
    CompetencyId      INT NOT NULL,

    CONSTRAINT PK_EmployeeCompetencies
        PRIMARY KEY (EmployeeId, CompetencyId),

    CONSTRAINT FK_EmployeeCompetencies_Employee
        FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees(EmployeeId)
        ON DELETE CASCADE,

    CONSTRAINT FK_EmployeeCompetencies_Competency
        FOREIGN KEY (CompetencyId)
        REFERENCES dbo.Competencies(CompetencyId)
        ON DELETE CASCADE
);
GO

CREATE TABLE dbo.Shifts
(
    ShiftId           INT IDENTITY(1,1) PRIMARY KEY,
    ShiftDate         DATE NOT NULL,
    ShiftType         NVARCHAR(20) NOT NULL,
    Hours             DECIMAL(4,2) NOT NULL,
    MinimumStaff      INT NOT NULL,

    CONSTRAINT CK_Shifts_ShiftType
        CHECK (ShiftType IN ('Dag', 'Kveld', 'Natt')),

    CONSTRAINT CK_Shifts_Hours
        CHECK (Hours > 0 AND Hours <= 24),

    CONSTRAINT CK_Shifts_MinimumStaff
        CHECK (MinimumStaff > 0)
);
GO

CREATE TABLE dbo.ShiftRequirements
(
    ShiftId           INT NOT NULL,
    CompetencyId      INT NOT NULL,
    MinimumCount      INT NOT NULL
        CONSTRAINT DF_ShiftRequirements_MinimumCount DEFAULT 1,

    CONSTRAINT PK_ShiftRequirements
        PRIMARY KEY (ShiftId, CompetencyId),

    CONSTRAINT FK_ShiftRequirements_Shift
        FOREIGN KEY (ShiftId)
        REFERENCES dbo.Shifts(ShiftId)
        ON DELETE CASCADE,

    CONSTRAINT FK_ShiftRequirements_Competency
        FOREIGN KEY (CompetencyId)
        REFERENCES dbo.Competencies(CompetencyId),

    CONSTRAINT CK_ShiftRequirements_MinimumCount
        CHECK (MinimumCount > 0)
);
GO

CREATE TABLE dbo.ShiftAssignments
(
    ShiftId           INT NOT NULL,
    EmployeeId        INT NOT NULL,

    CONSTRAINT PK_ShiftAssignments
        PRIMARY KEY (ShiftId, EmployeeId),

    CONSTRAINT FK_ShiftAssignments_Shift
        FOREIGN KEY (ShiftId)
        REFERENCES dbo.Shifts(ShiftId)
        ON DELETE CASCADE,

    CONSTRAINT FK_ShiftAssignments_Employee
        FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees(EmployeeId)
);
GO

CREATE INDEX IX_Shifts_ShiftDate
ON dbo.Shifts(ShiftDate);
GO

CREATE INDEX IX_ShiftAssignments_EmployeeId
ON dbo.ShiftAssignments(EmployeeId);
GO

CREATE INDEX IX_EmployeeCompetencies_CompetencyId
ON dbo.EmployeeCompetencies(CompetencyId);
GO
