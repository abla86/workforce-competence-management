namespace Workforce.Api.DTOs;

public sealed record CreateEmployeeRequest(string Name, string Role, decimal PositionPercent);
public sealed record UpdateEmployeeRequest(string Name, string Role, decimal PositionPercent, bool IsActive);
public sealed record AddCompetenceRequest(int CompetenceId, string Level, DateOnly? ValidUntil);
public sealed record CreateCompetenceRequest(string Name, string Category);
public sealed record CreateShiftRequest(DateOnly Date, string ShiftType, decimal Hours, int MinimumStaff);
public sealed record UpdateShiftRequest(DateOnly Date, string ShiftType, decimal Hours, int MinimumStaff);
public sealed record AssignEmployeeRequest(int EmployeeId);
public sealed record AddRequirementRequest(int CompetenceId, int MinimumCount, string MinimumLevel);
public sealed record SetEmployeeAvailabilityRequest(DateOnly Date, bool IsAvailable, string Reason);
