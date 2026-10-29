# Pathway — Agent Instructions (Codex CLI)

Read this file and the relevant files in `docs/` before editing code. `docs/` is the source of truth; do not silently invent requirements or change architectural decisions.

## Context

Pathway is an AI-assisted goal planner. Stack: Vue 3 + TypeScript; ASP.NET Core Web API (.NET 10); EF Core + PostgreSQL; xUnit + Vitest; Docker Compose.

## Mandatory workflow

1. Identify the user story and acceptance criteria affected.
2. Propose a small implementation plan and explain trade-offs before substantial changes.
3. Prefer one end-to-end vertical slice at a time.
4. Keep controllers thin; separate domain logic, external integrations and persistence.
5. Treat browser input, session cookies and LLM output as untrusted.
6. Require ownership checks for every goal, milestone and task operation.
7. Never log session tokens, API keys, or private planning answers.
8. Write or update tests for meaningful behavior changes. Run relevant checks when possible; report what was and was not run.
9. Update docs and ADRs when decisions change. Do not add patterns, libraries or infrastructure without a concrete need.
10. Never commit secrets or claim a feature/test works without verification.

## MVP constraints

- Anonymous cookie-based sessions; server stores only token hashes.
- Plan preview must not persist generated milestones/tasks; confirmation validates and schedules on the server.
- User-facing estimates are whole hours; internal storage is integer minutes.
- Sequential scheduling first; DAG is a stretch goal.
- LLM suggests work breakdowns; deterministic C# code assigns dates.
- Avoid unnecessary repository abstractions over EF Core.

## References

- `docs/product/prd.md`
- `docs/product/user-stories.md`
- `docs/architecture/overview.md`
- `docs/architecture/database.md`
- `docs/architecture/api.md`
- `docs/decisions/`

When implementation details are unresolved, state assumptions and request confirmation for high-impact choices.

