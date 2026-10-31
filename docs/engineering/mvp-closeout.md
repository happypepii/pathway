# Local MVP checkpoint and follow-up handoff — 2026-10-31

## Scope and ownership

The developer approved a final, small task-help prompt export and explicitly
authorized a local commit after MVP acceptance. The next phase is Mode B:
developer-led Engineering Deep Dive. Codex generated the MVP implementation,
tests and records under developer-provided requirements and model/scope choices.
The developer restored/configured the environment and selected the product
direction; independent implementation or debugging ownership is not attributed
to the developer. No remote push or public deployment is authorized by this
closeout.

The developer subsequently requested saving the current work as a checkpoint
and pausing discussion until their return. Technical journey/data acceptance for
US-01–US-09 is recorded below, but a newly reported goal-alignment failure remains
unresolved. This checkpoint does not claim reliable planning across arbitrary
goals. Public deployment also remains pending.

## Final task-help slice

`TaskHelpPrompt.vue` appears beside every preview/saved task. `taskPrompt.ts`
builds text from explicit goal/stage/task fields, without IDs, session tokens or
other API metadata. Optional goal descriptions and the three planning answers
are excluded by default and selected explicitly. The user can describe the
obstacle, inspect/edit the resulting prompt and copy its actual edited content.
Changing context selections regenerates the text, as stated in the UI.

The feature has no server endpoint, model call, persistence, external navigation
or automatic subgoal creation. Drafts are page-local; explicit clipboard copying
is user initiated. Clipboard failure focuses/selects the text for Ctrl+C/Command+C.
Context is JSON-serialized as data before fixed assistance instructions, but this
is not a guarantee that an external model will resist prompt injection or follow
the requested interaction style. Users review what they choose to share.

This provides a lightweight escape hatch for unclear tasks. A nested-goal design
would require parent/child scheduling, effort and progress rules; it is deferred
until actual use justifies it. No dependencies, database migrations or Backend
changes were added by the export slice.

## Acceptance evidence

- **14 frontend unit tests passed**, including context inclusion/exclusion,
  identifier filtering, legacy tasks and obstacle/data formatting.
- **9 synthetic Chromium cases passed**, plus the HTTPS HTTP journey through
  Backend/PostgreSQL and the explicit protocol fixture. New UI cases cover
  preview/saved help, opt-in context, edits, actual clipboard content, no API
  requests during prompt operations and mobile/manual-copy fallback.
- Reusing the long-running demo for a complete live suite produced **5 passed /
  1 blocked**: the last case could not bootstrap a session because earlier test
  runs exhausted the existing 20-per-IP/hour creation quota. The real Qwen case
  itself passed in that run (57,920 ms; 3 milestones / 9 tasks), including saved
  task context export. The subsequent **fresh isolated full suite passed 6/6**
  on ports 5176/7043. Its real Qwen request took **76,257 ms**, with three
  milestones/nine tasks and SQL/guidance/reload/ownership/task-help verification.
  The fresh test services/database were removed. No rate-limit relaxation or
  demo-data deletion was used; repeated direct tests should use fresh runners
  rather than consuming the long-running demo's anonymous-creation quota.
- Vue TypeScript/production build passed. The prior complete backend regression
  on this exact Backend implementation was **144 passed, 0 failed, 0 skipped**;
  Backend source did not change during the final export slice, so it was not
  rerun. Synthetic browser regression built the Backend successfully.
- Diff whitespace checks and script syntax checks passed. Pending files were
  reviewed before staging; certificates, `.env`, generated artifacts, browser
  recordings, model files and local test databases are not committed.

Earlier detailed evidence: [live MVP](mvp-live-acceptance.md),
[model experiments](ollama-local-validation.md), [visual path](visual-path.md),
[plan guidance](plan-guidance.md). These earlier records describe historical
checkpoints and their respective test counts.

## Retained environment and limitations

The current rich-plan demo remains on https://localhost:5174, with its isolated
PostgreSQL database. The older 5173 environment and all development data remain
untouched. Use a separate browser/profile to avoid mixing localhost session
cookies across ports. Test databases are disposable tmpfs, not durable storage.
The final synthetic regression cleaned up only its own test services/database.

Known remaining work is recorded rather than expanded into MVP scope:

- A user-reported case showed background experience displacing the primary goal
  in the generated plan. Structural/schema and persistence checks did not detect
  this semantic failure. The private example is not reproduced in this repository.
  Goal clarification and explicit user confirmation of direction are proposed
  for the next discussion, not implemented or accepted as a settled design.
- Public hosting/domain/HTTPS proxy, durable PostgreSQL/backups, persistent Data
  Protection keys and private inference hosting have not been provisioned.
- Qwen 3 is an integration candidate, not a final product model. Resource
  accuracy, language consistency, estimate quality and multi-user capacity are
  not established by the synthetic goals or single-request timing observations.
- Real 90-second cancellation/model-unavailable browser scenarios remain
  unverified beyond the existing adapter/synthetic failure coverage.
- No dependencies/DAG, recurring-task scheduling, nested subgoals, active-plan
  regeneration, automatic external-answer import or anonymous-data cleanup job.

## Resume discussion before beginning the next phase

First revisit goal understanding, separation of background from intent, and
whether a bounded clarification dialogue plus user-confirmed goal summary is
appropriate. JSON is a context representation, not evidence of semantic
understanding; no format-comparison experiment establishes its superiority.
The user requested no further implementation/discussion until their return.

## Possible later Deep Dive: developer-led concurrency investigation

Start with the existing requirement that concurrent confirmation must create one
plan atomically. Establish a reproducible baseline and explain the guarantees
before proposing changes; no defect or performance improvement is assumed.

Read `PlanningService.Confirm` and
`PlanningEndpointTests.Concurrent_confirmations_create_exactly_one_plan`.
The existing focused baseline can be reproduced using:

```bash
bash scripts/test-persistence.sh --filter FullyQualifiedName~Concurrent_confirmations_create_exactly_one_plan
```

The first developer exercise is to predict the two HTTP responses and resulting
database state when two requests confirm the same draft/version, then sketch a
possible interleaving. Codex should review that explanation and help design the
next experiment rather than immediately supplying the full implementation.
No developer-led investigation results are claimed yet.
