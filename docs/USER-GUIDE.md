# Workforce & Competence Management — User Guide

## Purpose

The application combines three operational areas in one workflow:

1. Employees and availability
2. Competence and competence validity
3. Shift planning and staffing coverage

The coverage engine evaluates whether an assigned workforce satisfies staffing and competence requirements and returns an operational status.

## Recommended workflow

### 1. Employees

Create active employees with role and position percentage. Maintain employee competence records and validity dates.

Deactivate an employee rather than deleting historical scheduling data when assignments exist.

### 2. Competence

Create the competence catalogue. For each employee, record the competence level and optional validity/expiry date.

Validity states used by the application include `ACTIVE`, `REVIEW_DUE` and `EXPIRED`.

### 3. Shift planning

Create a shift with date, shift type, start time, duration, department, minimum staffing and critical/non-critical flag.

Add competence requirements with minimum count, minimum level, optional required role and critical flag.

### 4. Staffing

Use candidate ranking before assigning an employee. The planner considers competence, absence, overlap, rest-period and workload constraints.

An ineligible candidate must not be assigned through the protected assignment endpoint.

### 5. Coverage

Run coverage analysis after staffing changes. The result includes overall status, staffing coverage, competence coverage, missing staff, uncovered requirements, warnings and explanatory reasons.

### 6. What-if analysis

Use scenario endpoints to simulate removal/absence without changing the stored shift plan. Scenario results are non-destructive.

### 7. Dashboard

The dashboard provides operational indicators for active employees, competence inventory, RED/YELLOW shifts, expiring competences and upcoming shifts.

## Status interpretation

| Status | Meaning | Operational response |
|---|---|---|
| GREEN | Required staffing and competence coverage is satisfied | Ready for normal review |
| YELLOW | Coverage is usable but warnings or non-critical gaps exist | Review before publishing |
| RED | Critical staffing/competence requirement is not satisfied | Action required before relying on the plan |

## Safety principle

The application is decision support. A GREEN result does not replace professional, legal, contractual or local staffing judgement. Managers remain responsible for final staffing decisions.

## Authentication

All `/api/*` endpoints except authentication endpoints require authentication. The local/demo bootstrap flow is intended for controlled setup, not for production identity management.
