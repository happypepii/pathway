# User Stories and Acceptance Criteria

**Status:** v0.1 proposal. A **user journey** spans the entire product; an **epic** groups related capabilities; each **user story** captures independently valuable behavior. Implementation tasks are not user stories.

## Epic: Goal planning and execution

### US-01 — Create a goal

As a user, I want to describe a goal and my time constraints so that I can begin planning.

- [ ] Title is required (1–200 characters).
- [ ] Target date must be today or later.
- [ ] Available hours/week is an integer between 1 and 168.
- [ ] A draft goal is saved under the current anonymous session.
- [ ] Invalid input returns a clear validation response.

### US-02 — Provide planning context

As a user, I want to answer a few relevant questions so that the plan fits my situation.

- [ ] System supports 2–3 guided questions.
- [ ] Optional questions can be skipped.
- [ ] Answers are saved only under a goal owned by the current session.

### US-03 — Generate a task breakdown

As a user, I want milestones and manageable tasks so that I know what to do.

- [ ] LLM output conforms to a validated structured schema.
- [ ] Every task has a title and positive whole-hour effort estimate.
- [ ] Limits are enforced on task count, nesting and field lengths.
- [ ] Provider errors/timeouts yield safe retryable errors.
- [ ] Generating a preview does not persist milestones/tasks.

### US-04 — Schedule work

As a user, I want a capacity-aware schedule so that deadlines are realistic.

- [ ] Server assigns start/end dates from task effort and weekly capacity.
- [ ] Tasks can span days; no capacity is over-allocated by the algorithm.
- [ ] Infeasible target dates are flagged, not silently accepted as feasible.
- [ ] Scheduling results are deterministic for identical inputs.

### US-05 — Review and confirm a plan

As a user, I want to edit AI suggestions before accepting them so that I remain in control.

- [ ] Preview allows editing task titles and whole-hour estimates and removing tasks.
- [ ] Confirmation revalidates input and recalculates dates on the server.
- [ ] Confirmation persists milestones and tasks transactionally.
- [ ] A failed confirmation leaves no partial plan.

### US-06 — Track progress

As a user, I want to mark tasks complete and see the next step so that I can keep moving.

- [ ] Dashboard shows goal status, completion percentage and next incomplete task.
- [ ] A task can be marked complete and reopened.
- [ ] Progress is derived from saved task completion state.
- [ ] A different anonymous session cannot read or change the task.

## Cross-cutting acceptance criteria

- [ ] Every goal-scoped operation enforces anonymous session ownership.
- [ ] No session tokens or LLM credentials appear in logs or browser-readable storage.
- [ ] Core scheduling behavior and authorization boundaries have automated tests.
- [ ] Public deployment uses HTTPS and a consistent origin for cookie-based API requests.

## Stretch goal — DAG

- [ ] Task dependencies form an acyclic directed graph.
- [ ] Self/cross-goal dependencies and cycles are rejected.
- [ ] Topological ordering and dependency-aware scheduling have unit tests.
