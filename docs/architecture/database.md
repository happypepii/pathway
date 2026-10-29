# Database Schema — v0.1

**Database:** PostgreSQL; **ORM:** EF Core. Phase 02 migrates `anonymous_sessions` and `goals` only. EF Core migrations under `backend/Pathway.Api/Infrastructure/Persistence/Migrations` are the executable schema source. Planning tables below remain proposed and are not migrated.

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
| token_hash | varchar(64) | unique, not null; CHECK exactly 64 hexadecimal characters; SHA-256 hex of random token |
| created_at | timestamptz | not null |
| last_seen_at | timestamptz | not null |
| expires_at | timestamptz | not null; indexed |

Raw tokens must never be persisted or logged. Session expiry is fixed at 30 days from creation, without sliding expiration. Expired sessions are rejected immediately; scheduled cleanup is deferred.

## goals

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| session_id | uuid | FK → anonymous_sessions.id; indexed; not null |
| title | varchar(200) | not null; nonempty; no leading/trailing PostgreSQL POSIX whitespace |
| description | text | nullable; CHECK maximum 2,000 characters |
| target_date | date | not null; C# DateOnly, validated against user IANA timezone |
| available_hours_per_week | integer | not null, CHECK 1..168 |
| status | varchar(20) | not null; CHECK Draft / Active / Completed / Archived; entity initializes Draft |
| created_at | timestamptz | not null |
| updated_at | timestamptz | not null |

## planning_answers (future phase)

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| goal_id | uuid | FK → goals.id; not null |
| question_key | varchar(100) | not null |
| question | text | not null |
| answer | text | nullable |

Unique `(goal_id, question_key)`.

## milestones (future phase)

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| goal_id | uuid | FK → goals.id; not null |
| title | varchar(200) | not null |
| sort_order | integer | not null |

Unique `(goal_id, sort_order)`.

## tasks (future phase)

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

- Session → goals uses `DeleteBehavior.Restrict` / `ON DELETE RESTRICT`; delete dependent goals explicitly before deleting a session.
- In future migrations, deleting a goal cascades to its planning answers, milestones and tasks.
- Expired anonymous sessions are rejected immediately. Scheduled cleanup of sessions and associated data is deferred; database expiry is not automatic.
- Goal ownership is checked through `goals.session_id`; tasks are checked through `tasks → milestones → goals`.
- Use a transaction for confirming/replacing a plan; never save half of a plan.

## Indexes

`anonymous_sessions(token_hash)` unique, `anonymous_sessions(expires_at)`, `goals(session_id)`, `planning_answers(goal_id, question_key)` unique, `milestones(goal_id, sort_order)` unique, `tasks(milestone_id, sort_order)` unique.

## Phase 02 implementation boundaries

- UUIDs are assigned in the application. Timestamps are explicitly supplied UTC `DateTime` values mapped to `timestamptz`; PostgreSQL stores instants, not the user's timezone. PostgreSQL timestamp precision is microseconds.
- Entities hold `TokenHash` only; token generation, hashing workflows, cookies and authentication are not implemented here. Future session creation must set expiry to creation + 30 days; last-seen updates must not extend it.
- No time-dependent target-date check exists in SQL. Future application validation uses the user's IANA timezone; no timezone column is added in this phase.
- GoalStatus is a C# enum stored as constrained text. Goal titles are rejected if untrimmed; persistence does not silently normalize input. PostgreSQL character counts/POSIX whitespace are integrity checks; future application validation must define consistent user-facing string rules.
- EF mappings explicitly name snake_case columns. Sensitive-data logging is disabled by default and is not enabled. There are no generic repositories, automatic startup migrations, or cleanup jobs.
- See the root README for explicit migration commands and isolated real-PostgreSQL integration tests.
