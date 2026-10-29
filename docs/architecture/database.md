# Database Schema — Proposed v0.1

**Database:** PostgreSQL; **ORM:** EF Core. Names below are logical table/column names; migrations will be the executable schema source once implemented.

## Relationships

```text
anonymous_sessions (1) ── (N) goals
                              ├── (N) planning_answers
                              └── (N) milestones ── (N) tasks

Future: tasks (N) ── task_dependencies ── (N) tasks
```

## anonymous_sessions

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| token_hash | varchar(64) | unique, not null; SHA-256 hex of random token |
| created_at | timestamptz | not null |
| last_seen_at | timestamptz | not null |
| expires_at | timestamptz | not null; indexed |

Raw tokens must never be persisted or logged. Session expiry is fixed at 30 days from creation, without sliding expiration. Expired sessions are rejected immediately; scheduled cleanup is deferred.

## goals

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| session_id | uuid | FK → anonymous_sessions.id; indexed; not null |
| title | varchar(200) | not null |
| description | text | nullable; maximum 2,000 characters for goals |
| target_date | date | not null; C# DateOnly, validated against user IANA timezone |
| available_hours_per_week | integer | not null, CHECK 1..168 |
| status | varchar(20) | Draft / Active / Completed / Archived |
| created_at | timestamptz | not null |
| updated_at | timestamptz | not null |

## planning_answers

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| goal_id | uuid | FK → goals.id; not null |
| question_key | varchar(100) | not null |
| question | text | not null |
| answer | text | nullable |

Unique `(goal_id, question_key)`.

## milestones

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| goal_id | uuid | FK → goals.id; not null |
| title | varchar(200) | not null |
| sort_order | integer | not null |

Unique `(goal_id, sort_order)`.

## tasks

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| milestone_id | uuid | FK → milestones.id; not null |
| title | varchar(200) | not null |
| description | text | nullable |
| estimated_minutes | integer | not null; CHECK > 0 |
| scheduled_start_date | date | nullable |
| scheduled_end_date | date | nullable |
| sort_order | integer | not null |
| completed_at | timestamptz | nullable |

Unique `(milestone_id, sort_order)`. Check end date >= start date if both exist. MVP accepts estimates in whole hours at API/UI boundary and converts to minutes.

## Future: task_dependencies (not migrated in MVP)

| Column | Type | Constraint |
|---|---|---|
| task_id | uuid | FK → tasks.id |
| depends_on_task_id | uuid | FK → tasks.id |

Composite PK `(task_id, depends_on_task_id)`; CHECK task_id != depends_on_task_id. Domain logic must additionally enforce same-goal membership and acyclicity.

## Delete and isolation rules

- Deleting a goal cascades to its planning answers, milestones and tasks.
- Expired anonymous sessions are rejected immediately. Scheduled cleanup of sessions and associated data is deferred; database expiry is not automatic.
- Goal ownership is checked through `goals.session_id`; tasks are checked through `tasks → milestones → goals`.
- Use a transaction for confirming/replacing a plan; never save half of a plan.

## Indexes

`anonymous_sessions(token_hash)` unique, `anonymous_sessions(expires_at)`, `goals(session_id)`, `planning_answers(goal_id, question_key)` unique, `milestones(goal_id, sort_order)` unique, `tasks(milestone_id, sort_order)` unique.
