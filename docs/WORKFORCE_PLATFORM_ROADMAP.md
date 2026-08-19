# Workforce Platform Roadmap

## Purpose

Develop `workforce-competence-management` into a production-oriented workforce planning platform for municipal health and care services.

The repository must distinguish implemented functionality from planned functionality. No feature is described as implemented until it exists and is covered by tests where practical.

## Product areas

### 1. Workforce master data
- Employees and employment relationships
- Position percentage / FTE
- Role and department
- Skills and competence requirements
- Qualifications and authorization status
- Availability and work restrictions

### 2. Shift planning
- Shift definitions
- Staffing requirements
- Planned assignments
- Vacancies
- Weekend/night planning
- Schedule conflict detection

### 3. Competence-based matching
A candidate for a shift should be evaluated against:
- availability
- role requirements
- required competencies
- qualification validity
- workload and scheduling conflicts

Matching must be explainable: the system should show why a person is eligible, ineligible, or lower priority.

### 4. Absence and coverage
- Vacation
- Sick leave
- Leave of absence
- Other unavailable periods
- Automatic impact on staffing coverage

### 5. Management dashboard
- Current staffing coverage
- Critical shifts
- Competence gaps
- Vacancies
- Absence impact
- FTE and staffing trends

### 6. Reporting and analytics
- Staffing coverage over time
- Competence coverage
- Unfilled shifts
- Overtime indicators
- Absence trends
- Department comparison

### 7. Security and governance
- Role-based access control
- Audit logging
- Least-privilege design
- Data minimisation
- Secure configuration
- No real sensitive employee data in development/demo data

## Engineering goals

- Clear domain/application/infrastructure boundaries
- SOLID principles where appropriate
- EF Core and relational persistence
- REST API with OpenAPI
- Automated unit/integration tests
- CI/CD
- CodeQL and dependency security
- Docker
- Observability and structured logging
- Future Azure deployment

## Development order

1. Establish domain model and invariants
2. Employee/competence model
3. Shift and staffing requirement model
4. Availability and absence
5. Matching engine
6. Dashboard and reporting APIs
7. Authentication/authorization
8. Audit and security hardening
9. Integration and end-to-end testing
10. Deployment and operational documentation
