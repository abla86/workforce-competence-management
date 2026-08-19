# Workforce & Competence Platform — Product Requirements

## Product goal

The application shall help a staffing planner answer one question quickly:

> Do we have enough people with the right role, authorization and valid competence to safely perform the work planned for each shift?

The system is decision support. It must make recommendations and warnings explainable; final staffing decisions remain with an authorized human user.

## Core concepts

### Employee
- role
- authorization where relevant
- employment percentage/FTE
- active status
- competences and validity
- availability/absence
- preferences
- assigned shifts

### Shift
- date/time
- shift type
- department/location
- minimum staffing
- required role distribution
- work tasks
- competence requirements
- assignments
- status

### Work task
A task represents work that must be performed during a shift.

A task may have:
- required role
- required authorization
- required competence
- minimum level
- required count
- optional/safety-critical classification

Tasks may be entered manually or suggested from configurable templates/lists. Automatically suggested tasks must be presented as suggestions and explicitly accepted/edited by a user before becoming authoritative requirements.

## Shift validation

Every shift should be evaluated against:

1. Minimum number of staff
2. Required role distribution
3. Required authorization
4. Required competence
5. Competence validity/expiry
6. Assignment conflicts
7. Availability/absence
8. Rest-period rules
9. Configured workload constraints
10. Critical task coverage

The result must be explainable.

Example:

```text
RED — Not approved

Staffing:             3 / 3       OK
Registered nurses:    1 / 1       OK
Medication competence:1 / 1       OK
Acute competence:     0 / 1       MISSING
Critical task:        uncovered   MISSING
```

## Status model

- GREEN: all configured requirements satisfied
- YELLOW: staffing is operationally possible but one or more warnings/risks exist
- RED: one or more mandatory requirements are not satisfied

A status must never hide the underlying reasons.

## Candidate replacement

For an uncovered shift, candidates should be ranked using explainable factors:

- eligibility
- required role
- authorization
- competence and validity
- availability
- scheduling conflicts
- rest-period constraints
- workload/fairness indicators
- employee preferences

The system must distinguish hard constraints from preferences. A hard safety/qualification failure must not be silently traded for a higher preference score.

## Fairness and workload

The platform should expose distribution indicators for:
- total hours
- evenings
- nights
- weekends
- consecutive shifts
- undesirable shifts
- overtime indicators

Fairness should be transparent and configurable rather than represented as an unexplained score.

## Absence and changes

A change in availability or absence should automatically identify affected shifts and show the operational consequences.

The planner should be able to request:

> Find replacement

and receive qualified, explainable candidates.

## Scenario planning

The system should support non-destructive simulations such as:

- What happens if an employee is absent?
- What happens if a required competence is unavailable?
- Which assignments would resolve the largest number of uncovered requirements?

Simulations must not modify the real schedule until explicitly confirmed.

## Auditability

Material staffing changes should record:
- who changed it
- when
- what changed
- reason/comment where required

## User experience principles

- Action-oriented dashboard
- Minimal navigation for common tasks
- Search and filtering before deep navigation
- Inline editing where safe
- Bulk operations for repetitive administrative work
- Clear distinction between errors, warnings and information
- Keyboard-accessible controls
- Responsive layout
- No unexplained algorithmic recommendations
- No automatic overwrite of human decisions

## Configuration and task templates

The system should support configurable templates for recurring work settings. Templates may contain suggested tasks and competence requirements.

Example:

`Home care — evening shift`

may suggest:
- medication administration
- documentation
- personal care
- wound care
- nutrition
- acute assessment

The responsible user must be able to add, remove or change tasks and requirements before they are used for formal validation.

## Acceptance principle

A feature is not considered complete merely because data can be entered. It is complete when:

1. The user can perform the intended task efficiently.
2. The backend validates the relevant domain rules.
3. Important decisions are explainable.
4. Errors and unsafe states are visible.
5. Automated tests cover the critical rule paths.
6. The documentation accurately describes implemented behaviour.
