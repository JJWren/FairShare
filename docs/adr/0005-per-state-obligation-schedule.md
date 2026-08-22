# ADR 0005 — Each calculator resolves its own state's obligation schedule

**Status:** Accepted — 2026-08-22

## Context

The shared `BaseChildSupportCalculator` calls Alabama's schedule directly and unconditionally: `BcsoLookup` is a static, Alabama-only class, the base `Calculate()` validates every form's child count against Alabama's 1–6 range, and the above-ceiling error message names "the Alabama schedule ($30,000)" regardless of form. That was invisible while Alabama was the only state.

Oregon (the second state, per OAR 137-050) breaks every one of those assumptions, and not just with different numbers — the *semantics* differ:

- Oregon's scale covers 1–10 children (more than 10 uses the 10-child column); Alabama's schedule covers 1–6.
- Between brackets, Alabama rounds combined income to the nearest $50 row (`MAX(250, ROUND(cagi/50)*50)`); Oregon drops to the **lower** bracket, never rounding up.
- Above the top bracket, Alabama's guidelines leave support to the court, so FairShare raises `INCOME_ABOVE_SCHEDULE` (ADR 0001); Oregon **caps at the $30,000 row** and continues, rebuttably.
- Oregon's bottom rows are a flat $50 for any child count; Alabama floors to the $250 row.

## Decision

Obligation-schedule lookup becomes a per-state abstraction that each calculator resolves, supplying its own table data, child-count range, bracket-selection semantics, and above-ceiling behavior (error vs. cap). The base calculator keeps only the state-agnostic worksheet flow; schedule-boundary validation messages come from the state's schedule, not from shared code. Alabama's behavior must not change — the existing golden and workbook-oracle suites are the proof.

The stale Alabama-flavored defaults in shared types (`CalculationResult.State/Form`) and the UI's `CS42` fallback leave shared code in the same refactor.

## Consequences

- Adding a state becomes purely additive: schedule data + calculator class + DI registration, with no base-class edits.
- Bracket-selection and ceiling semantics live next to the state that owns them, where a reviewer can check them against that state's rule text.
- The `States`/`Forms` enums widen per state, as `src/FairShare.Domain/README.md` already prescribes.

## Alternatives considered

- **Branch by state inside the base class.** Grows a conditional per state and keeps every state's semantics tangled in one method; exactly the shape that produced the "Alabama schedule" error message on non-Alabama forms.
- **One merged lookup with unified semantics.** There are no unified semantics to implement — nearest-row vs. lower-row selection and error vs. cap at the ceiling are contradictory by rule, so a shared implementation must falsify at least one state.
