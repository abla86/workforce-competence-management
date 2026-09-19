# Repository Audit — 2026-08-28

## Purpose

This document establishes an auditable record for repository changes. It is intended to prevent undocumented or assumed changes from being presented as intentional work.

## Repository

`abla86/shift-competence-planner`

## Audit finding

The audited period contains runtime data-integration, CI, navigation and documentation changes. Historical intent must be established from the corresponding work record rather than inferred from commit titles.

## Evidence rule

A commit title is not sufficient evidence of authorisation. For each material change, the preferred evidence chain is:

1. user/requested scope or approved task;
2. working record describing the intended change;
3. Git commit and file-level diff;
4. test/build/verification result;
5. README/status claim consistent with the verified state.

## Verification rule

A change is not described as fixed, complete, production-ready, secure, tested, or verified unless the corresponding evidence exists.

## Historical integrity

This document does not rewrite Git history. Historical changes remain traceable through Git commits and diffs. Where an earlier change was later reverted, the audit record should retain both events.

## Current limitation

This audit records repository evidence available on 2026-08-28. It does not claim that a historical change was authorised when the original task/work record cannot establish that fact.

## Next audit action

Material historical changes should be reconciled against the available working records before any further functional alteration is made.
