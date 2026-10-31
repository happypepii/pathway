# Explanations, key points and resource suggestions — 2026-10-31

## Motivation and contribution

The developer requested plans that explain the small tasks shown on the visual
path and offer learning resources. The previous view contained titles, effort,
dates and progress only. Codex generated implementation, tests, migration and
documentation under developer direction. The initial resource policy follows the
communicated assumption of names/search directions rather than verified URLs;
no search service or paid integration was added. Developer review remains pending.

## Contract and persistence

US-08 and ADR-005 record bounds and trade-offs. Stage metadata contains purpose
(`description`), `outcome` and up to two resource objects (name/purpose/searchQuery).
Tasks contain instructions (`description`) and up to three key points. New model
output requires nonempty guidance. Legacy confirmation requests may omit it, but
all supplied fields are validated. Resource HTTP(S)/www URL text is rejected and
provider unknown fields are disallowed, including nested resource objects.

The deterministic scheduler carries guidance through its result without changing
date/capacity allocation. Confirmation persists it in the existing transaction.
Task descriptions reuse the existing column; key points use text[]. Stage
descriptions/outcomes are nullable columns, and bounded ordered resource metadata
uses JSONB via explicit serialization. No new package, resource table, controller,
external fetch or repository abstraction was needed.

The additive migration `20261031042128_PlanGuidance` preserves original rows and
gives old plans nullable descriptions/outcomes and empty metadata arrays. It is
not applied automatically or to the developer's database. Old plans render
normally without requiring regeneration or resetting progress.

## UI behavior

Preview and saved plans display the same stage explanation/outcome and task
instructions/key points. Resource panels expand to show names, purposes and
keywords, labeled unverified. A fixed Google search URL with an encoded query is
constructed only for user-selected navigation; no model href is trusted and no
query is automatically submitted. The path shows a short task description while
its node still focuses the existing editor/checkbox and full guidance below.

The confirmation request now forwards all metadata instead of dropping it when
projecting editable titles/estimates. Stage/task guidance is reviewable; only the
existing title/hour edits are exposed in this slice. Browser rendering uses plain
Vue interpolation, not generated HTML/Markdown execution.

## Executed verification

- **144 backend tests passed, 0 failed, 0 skipped**, on an independent disposable
  PostgreSQL project. Includes bounds/missing guidance/URL validation, resource
  unknown fields, unchanged scheduling dates, preview without writes, metadata
  round trip through confirmation/GET/direct EF reads, and a real migration
  upgrade from seeded old milestone/task rows. Upgrade preserves task IDs, notes
  and effort and supplies empty resource/key-point values.
- **7 synthetic Chromium cases passed**, plus the HTTPS HTTP journey. Includes
  the existing full journey/failures/conflict cases, guidance display/search href,
  roadmap progress/reload, mobile long text and legacy metadata-free plans.
- **4 live-mode Chromium cases passed**, using the actual adapter/model for the
  first case and explicit deterministic preview fixtures for the three UI cases.
  Actual Qwen preview: **59,282 ms**, **3 milestones / 9 tasks**. No live response
  was mocked. Direct SQL verified stage descriptions/outcomes/resources and task
  descriptions/key points, exact titles/minutes/dates, Draft no-write preview and
  Active persistence. Reload, completion, ownership and CSRF assertions passed.
- A final SQL inspection found **3 stage purposes, 3 outcomes, 3 resource
  suggestions, 9 task descriptions and 18 key points** in the live test plan.
- **10 frontend unit tests passed**; Vue TypeScript/production build passed;
  Backend build passed with zero warnings/errors. Shell/Python syntax and diff
  whitespace checks passed.
- Three deterministic UI cases were rerun to capture/inspect synthetic-only
  desktop and mobile screenshots, including an expanded resource panel. No
  private planning answers or session tokens were captured or logged.

The old demo remains on 5173/7040 with its original database (three migrations,
confirmed read-only). The new demo uses 5174/7041 and a separate test database
(four migrations). Synthetic regression used 5175/7042 and cleaned up only its
own test resources. Optional port overrides were added to the existing runner,
Vite and Playwright settings specifically to preserve the older environment.
Use an independent browser/profile for the newer demo: localhost cookies are
shared across ports. No existing development database or non-test resource was
deleted or migrated; no Git commit was made.

## Limitations and remaining decisions

Resource existence, relevance and correctness remain unverified; names may be
generic search directions rather than identifiable publications. Verified URLs
would need curated sources or a separate search/verification integration. The
model requested Traditional Chinese but the observed example mixed English
guidance with Chinese keywords; language consistency is not guaranteed.

Richer output took about 59 seconds in this single example, still under the
unchanged 90-second deadline. This is not a stable-latency or concurrency claim.
Recurring tasks/waiting periods remain outside the scheduler. Existing Active
plans do not gain guidance automatically, and regeneration is still deferred.
Public deployment and a final product-model decision remain pending.

Reproduce in a fresh isolated environment using `bash scripts/test-live.sh`, or
use the alternate-port command in README to preserve an existing demo. All
demonstration databases remain disposable tmpfs; production storage is not
provisioned by these commands.
