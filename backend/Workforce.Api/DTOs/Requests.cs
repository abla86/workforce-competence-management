namespace Workforce.Api.DTOs;

public sealed record CreateEmployeeRequest(string Name, string Role, decimal PositionPercent);
public sealed record UpdateEmployeeRequest(string Name, string Role, decimal PositionPercent, bool IsActive = true);
public sealed record AddCompetenceRequest(int CompetenceId, string Level, DateOnly? ValidUntil);
public sealed record CreateCompetenceRequest(string Name, string Category);
public sealed record CreateShiftRequest(DateOnly Date, string ShiftType, decimal Hours, int MinimumStaff, string? Department = null, TimeOnly? StartTime = null, bool IsCritical = false);
public sealed record UpdateShiftRequest(DateOnly Date, string ShiftType, decimal Hours, int MinimumStaff, string? Department = null, TimeOnly? StartTime = null, bool IsCritical = false, bool IsPublished = false);
public sealed record AssignEmployeeRequest(int EmployeeId);
public sealed record AddRequirementRequest(int CompetenceId, int MinimumCount, string MinimumLevel, string? RequiredRole = null, bool IsCritical = false);
public sealed record CreateAbsenceRequest(int EmployeeId, DateOnly From, DateOnly To, Models.AbsenceType Type, string? Note = null, bool Approved = true);
