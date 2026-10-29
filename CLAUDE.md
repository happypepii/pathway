# Pathway — Claude Code Instructions

## Project Overview

Pathway is an AI-powered personal goal planning application.

Its purpose is to help users break down overwhelming goals into actionable tasks and create realistic schedules based on their available time.

## Tech Stack

- Frontend: Vue 3, TypeScript, Vite, Tailwind CSS
- Backend: ASP.NET Core Web API (.NET 10)
- Database: PostgreSQL, Entity Framework Core
- Testing: xUnit, Vitest
- Infrastructure: Docker Compose

## Project Documentation

Read the relevant documentation before implementing features:

- Product requirements: `docs/product/prd.md`
- User stories: `docs/product/user-stories.md`
- System architecture: `docs/architecture/overview.md`
- Database design: `docs/architecture/database.md`
- API contracts: `docs/architecture/api.md`
- Architecture decisions: `docs/decisions/`

If documentation is missing or incomplete, ask for clarification rather than inventing requirements.

## Engineering Principles

1. Prefer simple, maintainable solutions.
2. Follow object-oriented design and SOLID principles where appropriate.
3. Use design patterns only when they solve a concrete problem.
4. Keep business logic separate from controllers and infrastructure.
5. Validate all external inputs, including LLM-generated content.
6. Do not introduce unnecessary abstractions, microservices, or infrastructure.
7. Write automated tests for business-critical logic.
8. Never commit secrets, credentials, or API keys.

## MVP Constraints

- Use anonymous sessions with secure HttpOnly cookies.
- Persist application data in PostgreSQL.
- Use deterministic scheduling logic in the backend.
- The minimum user-facing scheduling unit is one hour.
- AI generates task breakdowns, but does not directly write to the database.
- DAG-based scheduling is a future enhancement, not part of the initial MVP.

## Development Workflow

Before making significant changes:

1. Read the relevant documentation.
2. Explain the proposed implementation and trade-offs.
3. Prefer small, independently testable changes.
4. Add or update tests when changing business logic.
5. Run relevant tests and report the results.
6. Update documentation when architectural decisions change.

Do not claim tests passed unless they were actually executed.
