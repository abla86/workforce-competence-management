# Repository governance

## Purpose
This repository covers **shift and competence planning**.

## Integrity rules
- Preserve existing, historical and legacy functionality unless there is a documented technical, security, compatibility or legal reason to remove it.
- Distinguish current, historical, prototype and deprecated functionality.
- Documentation must describe actual implementation and must not claim completion or verification without evidence.
- Interfaces between UI, services, APIs, data and automation must use documented contracts where applicable.
- Tests and CI validate software behaviour; they do not by themselves establish scientific, clinical, regulatory or operational validity.
- External standards or methodologies must identify exact versions and authoritative sources when used.

## Change workflow
1. Inspect existing behaviour and dependencies.
2. Preserve compatible legacy behaviour.
3. Change the smallest necessary surface.
4. Update documentation and tests with code.
5. Run available checks.
6. Record unresolved limitations rather than hiding them.

## Audit boundary
This is a governance baseline, not a claim that every component has passed a complete functional audit.
