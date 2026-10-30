# User Stories and Acceptance Criteria

**Status:** MVP functionality implemented and backend-tested; live provider and browser acceptance remain pending (see docs/engineering/mvp-implementation.md). A **user journey** spans the entire product; an **epic** groups related capabilities; each **user story** captures independently valuable behavior. Implementation tasks are not user stories.

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

Checked items identify implemented behavior backed by backend/unit/HTTP integration evidence; they do not claim live-model generation or completed browser UI acceptance. The provider adapter has validated-schema/failure tests and a synthetic protocol fixture. A real provider/model must still be selected and exercised before declaring the MVP complete.
