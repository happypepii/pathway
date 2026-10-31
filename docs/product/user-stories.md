# User Stories and Acceptance Criteria

**Status:** Local MVP accepted with real Qwen 3 through Chromium/Backend/PostgreSQL; 126 backend tests passed. Public deployment remains pending (see docs/engineering/mvp-live-acceptance.md). A **user journey** spans the entire product; an **epic** groups related capabilities; each **user story** captures independently valuable behavior. Implementation tasks are not user stories.

## Epic: Goal planning and execution

### US-01 — Create a goal

As a user, I want to describe a goal and my time constraints so that I can begin planning.

- [x] Title is trimmed and required (1–200 characters); optional description is at most 2,000 characters.
- [x] Target date must be today or later in the user's IANA timezone (`DateOnly` / PostgreSQL `DATE`).
- [x] Available hours/week is an integer between 1 and 168.
- [x] A draft goal is saved under the current anonymous session.
- [x] Invalid input returns ValidationProblemDetails; successful creation returns 201 Created with Location.

### US-02 — Provide planning context

As a user, I want to answer a few relevant questions so that the plan fits my situation.

- [x] System supports 2–3 guided questions.
- [x] Optional questions can be skipped.
- [x] Answers are saved only under a goal owned by the current session.

### US-03 — Generate a task breakdown

As a user, I want milestones and manageable tasks so that I know what to do.

- [x] LLM output conforms to a validated structured schema.
- [x] Every task has a title and positive whole-hour effort estimate.
- [x] Limits are enforced on task count, nesting and field lengths.
- [x] Provider errors/timeouts yield safe retryable errors.
- [x] Generating a preview does not persist milestones/tasks.

### US-04 — Schedule work

As a user, I want a capacity-aware schedule so that deadlines are realistic.

- [x] Server assigns start/end dates from task effort and weekly capacity.
- [x] Tasks can span days; no capacity is over-allocated by the algorithm.
- [x] Infeasible target dates are flagged, not silently accepted as feasible.
- [x] Scheduling results are deterministic for identical inputs.

### US-05 — Review and confirm a plan

As a user, I want to edit AI suggestions before accepting them so that I remain in control.

- [x] Preview allows editing task titles and whole-hour estimates and removing tasks.
- [x] Confirmation revalidates input and recalculates dates on the server.
- [x] Confirmation persists milestones and tasks transactionally.
- [x] A failed confirmation leaves no partial plan.

### US-06 — Track progress

As a user, I want to mark tasks complete and see the next step so that I can keep moving.

- [x] Dashboard shows goal status, completion percentage and next incomplete task.
- [x] A task can be marked complete and reopened.
- [x] Progress is derived from saved task completion state.
- [x] A different anonymous session cannot read or change the task.

### US-07 — See the complete path

As a user, I want a visual path including every small task so that I can understand
how my next action leads toward my goal.

- [x] Preview and saved plans show the goal, ordered milestones and every task as connected nodes.
- [x] Nodes show whole-hour effort, scheduled dates and saved completion/next-step state.
- [x] Selecting a task node focuses its existing editor or completion control using mouse or keyboard.
- [x] Preview edits/removals update the path immediately; changed dates are identified as awaiting recalculation.
- [x] Saved task completion/reopening and refresh keep the path consistent with the task list.
- [x] The path is readable on mobile without page-level horizontal overflow.

Connections express existing sequential order, not newly introduced dependencies.

### US-08 — Understand each step and find learning resources

As a user, I want explanations, key points and resource suggestions so that I
understand how and why to carry out each part of my path.

- [x] New AI plans include a stage purpose/outcome, task descriptions/key points and stage resource suggestions.
- [x] Preview, confirmation and refresh preserve the same guidance alongside task edits.
- [x] Resource suggestions show names, purposes and search keywords, explicitly labeled unverified; the model does not supply URLs.
- [x] Server validates field lengths/counts and rejects URL-bearing resource suggestions.
- [x] Existing plans remain readable without regeneration; migration preserves their rows and supplies empty metadata.

The initial source policy is model-suggested names/search directions, not live web
research or verified citations. Search links are constructed by the frontend from
an encoded query and a fixed search-engine origin; no model-supplied URL is opened.

### US-09 — Get unstuck with a portable context prompt

As a user, I want an editable prompt for a task I cannot start so that I can ask
an external LLM for help without losing the context of my path.

- [x] Preview and saved tasks provide an expandable help panel carrying goal/stage/task guidance.
- [x] Optional goal description and planning answers are excluded by default and included only by explicit selection.
- [x] The user can describe the obstacle, edit the prompt and copy the actual edited content.
- [x] Clipboard failure provides a selected-text manual copy fallback, including on mobile/legacy plans.
- [x] Prompt generation/copy performs no API request, automatic external transmission, persistence or subgoal creation.

## Cross-cutting acceptance criteria

- [x] Every goal-scoped operation enforces anonymous session ownership.
- [x] No session tokens or LLM credentials appear in logs or browser-readable storage.
- [x] Core scheduling behavior and authorization boundaries have automated tests.
- [ ] Public deployment uses HTTPS and a consistent origin for cookie-based API requests. (Local HTTPS publish is verified; no public host is provisioned.)

## Stretch goal — DAG

- [ ] Task dependencies form an acyclic directed graph.
- [ ] Self/cross-goal dependencies and cycles are rejected.
- [ ] Topological ordering and dependency-aware scheduling have unit tests.

## Verification scope

Checked items identify implemented behavior backed by backend/unit/HTTP integration evidence, four historical synthetic Chromium cases and a real-Qwen Chromium journey. The live case verifies creation, answers, actual model preview, edited confirmation, direct SQL persistence, reload, completion and ownership. Public deployment must still be verified for the PRD's deployed-app success criterion. Neither single-model testing nor browser coverage exhausts plan quality or every UI state.
