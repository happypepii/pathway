# Database Schema — v0.1

**Database:** PostgreSQL; **ORM:** EF Core. Migrations include anonymous_sessions, goals, planning_answers, milestones and tasks. EF Core migrations under `backend/Pathway.Api/Infrastructure/Persistence/Migrations` are the executable schema source. All MVP tables below are implemented; task_dependencies remains deferred.

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
| time_zone_id | varchar(100) | not null; IANA timezone validated by application; legacy rows receive UTC |
| version | uuid | not null; application-managed EF concurrency token |
| status | varchar(20) | not null; CHECK Draft / Active / Completed / Archived; entity initializes Draft |
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
| description | varchar(1000) | nullable; stage purpose |
| outcome | varchar(500) | nullable; expected outcome |
| resources_json | jsonb | not null, default []; array of at most two resource suggestions |
| sort_order | integer | not null |

Unique `(goal_id, sort_order)`.

## tasks

| Column | Type | Constraint |
|---|---|---|
| id | uuid | PK |
| milestone_id | uuid | FK → milestones.id; not null |
| title | varchar(200) | not null |
| description | varchar(2000) | nullable; task instructions |
| key_points | text[] | not null, default empty array; at most three points |
| estimated_minutes | integer | not null; CHECK > 0 and divisible by 60 |
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
- Deleting a goal cascades to its planning answers, milestones and tasks.
- Expired anonymous sessions are rejected immediately. Scheduled cleanup of sessions and associated data is deferred; database expiry is not automatic.
- Goal ownership is checked through `goals.session_id`; tasks are checked through `tasks → milestones → goals`.
- Use a transaction for confirming/replacing a plan; never save half of a plan.

## Indexes

`anonymous_sessions(token_hash)` unique, `anonymous_sessions(expires_at)`, `goals(session_id)`, `planning_answers(goal_id, question_key)` unique, `milestones(goal_id, sort_order)` unique, `tasks(milestone_id, sort_order)` unique.

## Phase 02 implementation boundaries

- UUIDs are assigned in the application. Timestamps are explicitly supplied UTC `DateTime` values mapped to `timestamptz`; PostgreSQL stores instants, not the user's timezone. PostgreSQL timestamp precision is microseconds.
- Entities hold `TokenHash` only. Phase 03's first step adds a token utility (32 cryptographically random bytes, unpadded Base64url; SHA-256 over the encoded token's UTF-8 bytes, stored as hex) and a `TimeProvider`-based session policy. The policy sets expiry to creation + 30 days and treats `now >= ExpiresAt` as expired; last-seen updates must not extend it. The next step adds `AnonymousSessionService`: creation persists only the token hash, and read-only resolution rejects malformed, unknown or expired tokens without creating rows, updating last-seen, extending expiry or deleting expired data. The caller supplies an internally generated token for creation. These services are wired into `GET /api/v1/sessions/me` for read-only cookie resolution. POST /api/v1/sessions/anonymous now creates a row and issues its token solely through a Secure HttpOnly cookie. Valid-session reuse is read-only; invalid or expired cookies create a new row after CSRF and rate-limit checks. Expired rows are not removed.
- No time-dependent target-date check exists in SQL. Application validation uses the user's IANA timezone; time_zone_id is now persisted on goals.
- GoalStatus is a C# enum stored as constrained text. Goal titles are rejected if untrimmed; persistence does not silently normalize input. PostgreSQL character counts/POSIX whitespace are integrity checks; application validation trims titles and uses conservative UTF-16 length bounds before PostgreSQL character checks.
- EF mappings explicitly name snake_case columns. Sensitive-data logging is disabled by default and is not enabled. There are no generic repositories, automatic startup migrations, or cleanup jobs.
- See the root README for explicit migration commands and isolated real-PostgreSQL integration tests.

## Current planning persistence

Goal activation uses a row lock inside a transaction; the goal version and Draft state are checked before inserts. Completion and parent status are saved atomically. Planning answers have a 2,000-character check; tasks store positive minutes divisible by 60 and ordered unique positions. All IDs are application-assigned UUIDs. No new migration is automatically applied on startup.

`20261031042128_PlanGuidance` adds only the stage guidance fields and task
key_points. Existing rows receive null descriptions/outcomes and empty arrays;
original task descriptions and other planning data are retained. Resource objects
contain name/purpose/searchQuery; they are bounded display metadata serialized by
the application to JSONB, not separately addressable resources or verified links.
See ADR-005 and docs/engineering/plan-guidance.md for validation and upgrade tests.
