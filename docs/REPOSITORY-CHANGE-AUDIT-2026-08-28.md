# Repository Change Audit — 2026-08-28

## Scope
This document records the change-control standard and the findings established from repository history. It does not rewrite Git history.

## Repository
`abla86/workforce-competence-management`

## Verified finding
Functional, security, CI and documentation changes are present. Each material historical change must be reconciled with the work record before being treated as authorised.

## Required evidence chain
For every material change:
1. requested/approved scope;
2. working record describing intended work;
3. Git commit and file-level diff;
4. test/build/verification evidence;
5. README/status statement consistent with the evidence.

## Protected baseline
Existing project content is the baseline. A difference from another copy, older working document, or local checkout is **not** permission to overwrite, delete, move, refactor, reformat, replace dependencies, or alter architecture.

## No hidden scope
A task limited to one test means only that test is changed unless the user explicitly authorises additional work. Cleanup or improvements outside the requested scope are not implicitly authorised.

## Truthfulness rule
No claim of "fixed", "complete", "secure", "production-ready", or "verified" may be made without corresponding evidence.

## Historical integrity
If a change was made and later reverted, both events remain traceable. A revert does not erase the historical event.

## Audit limitation
Where the original working document or request is unavailable, this audit must mark authorisation as **not established**, rather than guessing.

## Freeze rule
Until an unexplained material change is reconciled with its originating work record, do not make further functional changes merely to make the repository appear consistent.
