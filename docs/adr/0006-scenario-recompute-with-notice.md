# ADR 0006 — Scenarios store inputs plus a stamped snapshot, and recompute with notice

**Status:** Accepted — 2026-08-22

## Context

Scenarios (named, saved worksheet-input snapshots — see CONTEXT.md) are FairShare's first persisted user data tied to a state and form; parent profiles are deliberately state-agnostic financial figures. The complication is that guideline rules move under saved data on a statutory schedule: Oregon re-sets its self-support reserve every July 1, so a scenario saved in June computes a different number in July under rules that are both "correct." Users bring these numbers to mediation and court; a saved number that silently changes — or one that is silently stale — is a trust failure either way.

## Decision

A Scenario stores its full worksheet inputs, the rule version it was computed under (the guideline effective date, e.g. "OAR 137-050 effective 2026-07-01"), and a snapshot of the result it produced. Reopening a scenario always recomputes under the **current** rules; when the recomputed result differs from the snapshot, the UI says so explicitly, showing both the saved and current figures and why they differ (rule version changed). FairShare calculates under current rules only — it does not maintain historical rule sets to re-run old versions.

The schema is state-agnostic from day one so Alabama scenarios adopt it unchanged.

## Consequences

- Scenario lists render from stored snapshots — no recompute fan-out to display them.
- Every state needs its rule parameters keyed by effective date so the stamp exists to compare against (Oregon's self-support reserve is the first annually-moving one).
- A reopened scenario is honest in both directions: the user keeps the number they saved *and* learns what today's rules say.

## Alternatives considered

- **Inputs only, silent recompute.** The number a user quoted in mediation moves without explanation the next July 1.
- **Frozen result, never recompute.** Permanently stale; useless for the planning and what-if purpose scenarios exist for.
- **Full historical rule engines.** Would let old scenarios re-run under their original rules, but courts apply current rules to new calculations; the snapshot preserves the historical number at a fraction of the cost.
