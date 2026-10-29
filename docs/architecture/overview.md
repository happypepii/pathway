# Architecture Overview

**Style:** Modular monolith with pragmatic layering. Avoid multiple deployable services for MVP.

```text
Browser (Vue 3 + TypeScript)
       | same-origin HTTPS, HttpOnly session cookie
       v
ASP.NET Core Web API
  - Session handling / ownership checks
  - Goal and Task application services
  - Planning orchestration
  - Deterministic scheduling domain logic
       |                         |
       v                         v
EF Core / PostgreSQL       LLM provider adapter
```

## Responsibilities

- **Frontend:** forms, guided questions, plan preview/editing, dashboard. No LLM API keys or scheduling authority.
- **Controllers:** HTTP binding, validation responses, authorization boundary, orchestration delegation.
- **Application services:** use cases, ownership verification, transactions and persistence coordination.
- **Scheduling domain:** pure deterministic functions; no HTTP, DB or LLM dependencies.
- **Infrastructure:** EF Core, session token hashing, external LLM provider integration.

## Design patterns

- **Adapter / dependency inversion:** `IPlanGenerator` isolates external LLM SDKs from application logic. Use when integration is implemented.
- **Strategy:** introduce `ISchedulingStrategy` only when a second scheduling algorithm exists; begin with one scheduling service.
- **Observer / domain events:** defer until multiple independently useful completion side effects exist.
- **Repository:** EF Core DbContext already provides repository/unit-of-work capabilities; avoid redundant generic repositories.

## Planning flow

1. Authenticated *anonymous session* creates a draft goal.
2. User saves planning answers.
3. Plan-preview endpoint calls LLM adapter and validates structured output.
4. Server schedules validated tasks and returns an editable preview; does not save tasks.
5. Confirmation endpoint validates edited data, recomputes schedule, and saves atomically.

## Security boundaries

- An anonymous session is a bearer credential, **not a verified user identity**.
- Store only hash of the opaque random session token in PostgreSQL.
- Verify ownership in every read/write path, including task endpoints via their goal.
- Use same-origin HTTPS, secure cookie flags, ASP.NET Core antiforgery protection for cookie-authenticated state changes, rate limits and safe error handling.
- Limit LLM output size and avoid storing/logging sensitive prompts unnecessarily.

## Testing

- xUnit: scheduling cases, validation, ownership isolation, persistence behavior.
- Vitest: frontend form and plan-editing behavior.
- Optional Playwright: end-to-end happy path after MVP works.

## Deployment

Local development: Docker Compose PostgreSQL. Production: HTTPS reverse proxy serving frontend and routing `/api` to backend under the same origin. Exact hosting TBD.
