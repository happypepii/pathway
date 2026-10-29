# Pathway

**Turn overwhelming goals into actionable plans.**

Pathway is an AI-assisted personal goal planning application. Users describe a goal, answer a few questions, review a generated task breakdown, receive a capacity-aware schedule, and track progress.

## Status

**Planning / MVP v0.1** — implementation not started. No functionality described below is claimed as complete.

## Tech stack

- Frontend: Vue 3, TypeScript, Vite, Tailwind CSS
- Backend: ASP.NET Core Web API (.NET 10), C#
- Persistence: PostgreSQL, Entity Framework Core
- Tests: xUnit, Vitest
- Local infrastructure: Docker Compose
- AI: provider-backed structured plan generation (provider not yet selected)

## MVP journey

1. Start an anonymous session.
2. Create a draft goal with a target date and available hours per week.
3. Answer short planning questions.
4. Generate and review a plan preview.
5. Edit tasks and confirm the plan.
6. View progress and mark tasks complete.

## Documentation

- [PRD](docs/product/prd.md)
- [User stories and acceptance criteria](docs/product/user-stories.md)
- [Architecture](docs/architecture/overview.md)
- [Data model](docs/architecture/database.md)
- [API contract](docs/architecture/api.md)
- [Architecture decisions](docs/decisions/)

## Development

Repository is designed for Windows + WSL2 (Ubuntu 24.04). Keep working files under the WSL filesystem (e.g. `~/projects/pathway`). Backend, frontend and Compose commands will be added once those components exist. Do not commit `.env` files or credentials.

## Non-goals for MVP

User accounts, cross-device recovery, calendar sync, notifications, complex task dependency scheduling, Kubernetes, Kafka and Redis.

## Engineering priorities

Security boundaries for anonymous sessions, deterministic scheduling, input validation, automated tests, transparent AI limitations, and small vertical slices. Design decisions are documented in `docs/decisions/`.

