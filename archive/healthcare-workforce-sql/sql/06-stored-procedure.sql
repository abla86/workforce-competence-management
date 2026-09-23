USE HealthWorkforceDb;
GO

CREATE OR ALTER PROCEDURE dbo.GetShiftStatusByDate
    @ShiftDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ShiftId,
        ShiftDate,
        ShiftType,
        MinimumStaff,
        AssignedStaff,
        StaffingStatus,
        StaffingDifference

    FROM dbo.vw_ShiftCoverage

    WHERE ShiftDate = @ShiftDate

    ORDER BY ShiftType;
END;
GO
