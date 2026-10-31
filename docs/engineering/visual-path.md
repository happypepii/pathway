# Complete visual path — 2026-10-31

**Follow-up:** explanations, key points and resource suggestions have subsequently
been implemented and verified; see [guidance evidence](plan-guidance.md). Pending
items below describe this earlier path-only checkpoint.

## Problem and scope

The developer requested a graph/tree-like path that includes small tasks, rather
than only milestone headings. They accepted a sequential route. Before this
slice, plans were editable lists and saved checkboxes, with no connected view.
US-07 now records the accepted path behavior. Explanations, key points and learning
resources remain a separate follow-up; their source/verification policy is not
decided by this UI change.

## Decision and contribution

Codex generated the Vue component, integration, browser tests and documentation
under the developer's product direction. Developer review remains pending. No
claim of developer-led implementation or measured performance improvement is made.

`PlanPath.vue` derives a connected ordered view directly from the plan's current
milestone/task arrays. Stage cards connect in sequence and each stage branches to
all of its task nodes. Global step numbers communicate order across stages.
Vue/CSS suffice for this fixed hierarchy; a graph-layout library would add a
dependency without a present need for arbitrary edges, dragging or auto-layout.
The existing sequential scheduler and API/database contracts are unchanged.

Both preview and saved plans show the path. Nodes expose task title, effort and
dates; saved plans also show completed/pending/next-step states. The next step is
the first incomplete task in the same ordering as the existing task list. A stage
is complete when all its tasks are complete. All-complete and reopen states are
derived rather than separately persisted.

Nodes are ordinary links with generated internal destinations and an explicit
focus action. Selecting one scrolls to and focuses the task title input or
completion checkbox. Ordered lists preserve reading order without introducing
an ARIA tree widget and its additional keyboard requirements. `aria-current=step`
identifies the next action, and labels/icons supplement color. Model text is
rendered using Vue interpolation; no model-provided HTML, SVG or links are used.

Desktop stages use a contained horizontal scroller; mobile stages stack
vertically. Long names wrap. Preview edits/removals update the graph immediately;
dates are marked as awaiting server recalculation after an edit. The graph does
not assign dates. Existing saved plans need no regeneration or migration.

## Verification

Actual executed checks:

- **Two new Chromium path cases passed** against the existing isolated Backend
  and PostgreSQL. The preview response is explicitly synthetic for deterministic
  UI behavior; normal persistence and task mutations still use the real Backend.
  Tests created/deleted only their own clearly marked test goals.
- Desktop case: every task appears; selection focuses the correct editor;
  title/effort edits and removal update nodes; confirmation, cross-stage next-step
  movement, saved reload, all-complete and reopening keep graph/list consistent.
- Mobile case at 390×844: stages stack, long unbroken titles wrap, nodes remain
  within viewport width, document has no horizontal overflow, and Enter on a
  focused node moves focus to the task editor.
- Desktop/mobile screenshots of synthetic test data were inspected locally.
  Normal test recording remains off; optional `PATHWAY_PATH_SCREENSHOTS=1` writes
  these synthetic-only images under `/tmp`, not into the repository.
- **One updated live-Qwen Chromium journey passed** (14.8 seconds total): actual
  preview 12,678 ms, four milestones/eight tasks. Preview and saved/reloaded graph
  each contained all eight tasks. Existing SQL comparison, confirmation,
  completion persistence, ownership and CSRF assertions passed. This is one
  observation with varying cache conditions, not a speed-improvement claim.
- **10 frontend unit tests passed**; Vue TypeScript/production build passed.
- Backend code was unchanged in this slice. The prior 126-test regression was
  not rerun; the live browser test did exercise the existing Backend/SQL path.

The live demo remains on https://localhost:5173. Refresh the page to load the Vue
changes, and use the original anonymous session to view its saved goals. The
existing disposable demo database was retained; no development data, model or
non-test resource was deleted, and no Git commit was created.

Reproduce deterministic cases against a running test environment:
`frontend/node_modules/.bin/playwright test --config frontend/playwright.config.ts path.spec.ts`.
Normal `scripts/test-e2e.sh` now selects the four existing journey cases plus the
two path cases; `scripts/test-live.sh` selects the live journey plus the two path
cases. The full synthetic suite was not rerun in this slice because the existing
live environment occupied its ports.

## Limits and next slice

Connections communicate existing sequential ordering and stage membership, not
parallelism or dependency constraints. There is no pan/zoom, drag/reorder, custom
edge creation or collapsed-node mode. Large plans require scrolling. Explanations,
key points and learning resources are not generated/persisted by this slice and
should be implemented end to end after the developer confirms their contract and
resource-source policy. No external dependency or ADR change was required.
