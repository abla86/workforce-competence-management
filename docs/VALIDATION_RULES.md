# Staffing Validation Rules

These rules define the first validation layer for staffing decisions. Safety and qualification constraints are hard constraints; preferences and fairness indicators must not override them.

## Mandatory checks

### Staffing count
`assigned staff >= minimum staff`

### Role coverage
For every mandatory role requirement:

`assigned eligible staff with role >= required count`

### Competence coverage
For every mandatory competence requirement:

`assigned staff with valid required competence and sufficient level >= required count`

### Authorization
Where a task or shift requires an authorization, the assigned person must have the required authorization.

### Competence validity
A competence with an expiry date is valid only through its configured validity date.

### Assignment conflict
An employee must not be assigned to overlapping shifts.

### Availability
An employee marked unavailable for the relevant period must not be treated as an eligible candidate.

### Rest period
The configured minimum rest period between relevant shifts must be respected.

## Severity

`RED`
- mandatory staffing requirement missing
- mandatory role requirement missing
- mandatory authorization missing
- mandatory competence missing/invalid
- assignment conflict
- mandatory rest-period violation
- critical work task uncovered

`YELLOW`
- preference conflict
- workload/fairness concern
- competence nearing expiry
- unusual staffing distribution
- other configured operational warning

`GREEN`
- all mandatory requirements satisfied
- no configured warning condition triggered

## Explainability

Every RED or YELLOW result must expose:
- rule identifier
- human-readable reason
- affected shift/task/person where applicable
- current value
- required value
- suggested next action where one exists

## Example

A shift requires three staff, one registered nurse, one employee with medication competence and one employee with acute assessment competence.

Three people are assigned, including one registered nurse and one person with medication competence, but nobody has acute assessment competence.

Result:

`RED — Mandatory competence requirement missing`

The shift is not considered fully covered even though the staffing count is 3/3.
