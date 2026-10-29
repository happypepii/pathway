# Pathway

**Turn overwhelming goals into actionable plans.**

Pathway is an AI-assisted personal goal planning application. Users describe a goal, answer a few questions, review a generated task breakdown, receive a capacity-aware schedule, and track progress.

## Status

**MVP v0.1 — Phase 01 scaffold.** Controller-based API host, xUnit project, Vue frontend and PostgreSQL Compose configuration exist. Goal planning, persistence integration and session authentication are not implemented.

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

Repository is designed for Windows + WSL2 (Ubuntu 24.04). Keep working files under the WSL filesystem (e.g. `~/projects/pathway`). See the scaffold instructions below. Do not commit `.env` files or credentials.

## Non-goals for MVP

User accounts, cross-device recovery, calendar sync, notifications, complex task dependency scheduling, Kubernetes, Kafka and Redis.

## Engineering priorities

Security boundaries for anonymous sessions, deterministic scheduling, input validation, automated tests, transparent AI limitations, and small vertical slices. Design decisions are documented in `docs/decisions/`.

## Run the Phase 01 scaffold

Prerequisites: .NET 10 SDK, Node.js 24 LTS with npm, and Docker with Compose v2 or later. Run commands from the repository root unless noted.

Start PostgreSQL:

```bash
cp .env.example .env
# Edit .env and choose a local development password before starting.
docker compose up -d --wait postgres
docker compose ps
docker compose exec -T postgres pg_isready -U pathway -d pathway
```

PostgreSQL 18 binds only to `127.0.0.1:5432`, with database/user `pathway`. Data persists in the Compose-managed `pathway_postgres_data` named volume, mounted at `/var/lib/postgresql` (PostgreSQL 18 layout). Stop with `docker compose down`; the volume is retained. The API is not connected to PostgreSQL yet; no entities or migrations exist. Changing the password in `.env` does not update credentials in an already initialized volume.

Build and run the API:

```bash
dotnet build backend/Pathway.slnx
dotnet run --project backend/Pathway.Api --launch-profile http
```

The development API listens on `http://localhost:5040`. Controllers are registered, but there are no application endpoints yet; `/` returns 404. The optional `https` launch profile also listens on `https://localhost:7040` and requires a trusted development certificate. Production retains HTTPS redirection; development HTTP is scaffolding only, not the production cookie policy.

Run the frontend in another terminal:

```bash
cd frontend
npm ci
npm run dev -- --host 127.0.0.1
```

Open the URL printed by Vite (normally `http://localhost:5173`). This is a static Pathway placeholder; API integration and same-origin request configuration are deferred until endpoints exist.

Build the frontend and invoke the test project:

```bash
npm --prefix frontend run build
dotnet test backend/Pathway.slnx
```

The xUnit project references the API but intentionally has no tests yet: no product behavior is implemented. Tailwind, Vitest, EF Core and its PostgreSQL provider will be added when their respective implementation work needs them. Never commit `.env` or real credentials.
