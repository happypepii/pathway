# ADR-004: MVP planning contracts and atomic activation

**Status:** Accepted within the delegated MVP scope, 2026-10-30

## Context

Goal validation needs a user's calendar date; preview must remain unsaved; simultaneous confirmation must not duplicate plans. The existing ADRs select sequential scheduling, PostgreSQL, and explicit confirmation.

## Decision

- Goal requests include an IANA timeZoneId (UTC is accepted). Store it with the goal and validate target dates against TimeProvider converted to that zone. Existing goals receive UTC in the additive migration.
- Draft goals can change all inputs. Active/Completed goals can edit title/description, but date, timezone and weekly capacity remain fixed so saved schedules remain consistent.
- Three fixed domain-general optional questions capture experience, desired outcome and constraints; answers are scoped to the owned goal.
- Plans contain 1–10 milestones and at most 100 tasks overall. Each milestone has at least one task, titles are trimmed and limited to 200 characters, effort is 1–1,000 whole hours per task. Answers/description are limited to 2,000 characters.
- Sequential allocation uses integer rational arithmetic: task start offset is floor(previousHours * 7 / weeklyHours); inclusive end offset is ceil(totalHours * 7 / weeklyHours) - 1. Tasks share day capacity; no per-day rounding is accumulated. Dates are allocation estimates, not appointments.
- Preview returns goalVersion. Goal edits and answer changes replace that UUID. Confirmation requires the same version and recalculates from the current local date; supplied dates are ignored.
- Confirm only a Draft. Lock the owned goal row inside an explicit PostgreSQL transaction, recheck version/status, then save milestones/tasks and Active status atomically. Concurrent/repeated confirmation returns 409.
- EF optimistic concurrency tokens guard other goal/task writes. Completion and parent status update in the same SaveChanges transaction. All complete means Completed; reopening means Active. Progress and next task are derived in milestone/task order.
- Regeneration/replacement after activation is deferred. It would require product decisions about preserving progress.
- Preview has a process-local limit of five requests per connection IP per minute. All existing session security controls remain in force.
- The IPlanGenerator adapter is isolated from scheduling and persistence. No paid provider is enabled or called automatically. An optional Ollama HTTP adapter is implemented; provider/model selection and live verification remain pending developer input.

## Alternatives and trade-offs

A UTC-only date check is simpler but violates local-calendar behavior. Per-day floating-point allocation can accumulate rounding errors. Serializing the short activation transaction prevents duplicate inserts without distributed locking. No generic repository, DAG or distributed infrastructure is necessary.

Holding a goal row lock during an LLM request would block edits unnecessarily, so generation is read-only and confirmation checks the preview version instead. Plan generation errors cannot alter a saved plan.

## Verification

See docs/engineering/mvp-implementation.md for actual tests, results, attribution and remaining limitations.
