# Workforce Domain Model

This document defines the initial domain boundaries for the workforce planning platform.

## Core aggregates

### Employee
Represents a person who can be scheduled. Employment details should be separated from identity data where practical.

Key concepts:
- EmployeeId
- employment relationship
- FTE / position percentage
- role
- department
- active/inactive status

### Competence
Represents a competence or qualification that may be required by a role, employee, or shift.

Key concepts:
- CompetenceId
- name
- category
- validity period where applicable
- required/optional classification

### EmployeeCompetence
Associates an employee with a competence and its validity.

### Shift
Represents a schedulable work period.

Key concepts:
- start/end
- shift type
- department
- staffing requirement
- status

### ShiftRequirement
Defines the competence and role requirements for a shift.

### ShiftAssignment
Represents the assignment of an employee to a shift.

### Availability / Absence
Represents periods during which an employee can or cannot be scheduled.

## Important invariants

1. An assignment must not overlap an unavailable period.
2. An assignment must not create an employee scheduling conflict.
3. Required competence must be valid at the time of the shift.
4. A shift should expose whether its staffing and competence requirements are fulfilled.
5. Matching decisions must be explainable and deterministic for identical input.
6. Domain rules must not depend directly on HTTP, UI, database, or infrastructure concerns.

## Future extensions

- fatigue/workload indicators
- collective-agreement constraints
- skill substitution rules
- multi-site staffing
- demand forecasting
- optimisation algorithms
