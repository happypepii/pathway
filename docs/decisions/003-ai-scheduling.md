# ADR-003: Separate AI Planning from Deterministic Scheduling

**Status:** Accepted for MVP

## Context

LLMs are useful for proposing milestones and tasks but are unreliable as the sole authority for dates, capacity constraints and database writes.

## Decision

Use a provider adapter to request a structured task breakdown, validate it on the server, then use deterministic C# scheduling logic to assign dates. Show an editable preview; save only after user confirmation and server-side recalculation. Whole hours at UI/API boundary, integer minutes internally.

## Alternatives

Ask the LLM to return final deadlines; persist LLM output immediately.

## Consequences

More backend logic but predictable, testable schedules and greater user control. MVP uses sequential scheduling with evenly distributed weekly capacity; dependency-aware DAG scheduling is a later extension.
