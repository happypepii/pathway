# Pathway

**Turn overwhelming goals into actionable plans.**

Pathway is an AI-assisted personal goal planning application. Users describe a goal, answer a few questions, review a generated task breakdown, receive a capacity-aware schedule, and track progress.

## Status

**MVP v0.1 — Phase 02 persistence.** EF Core entities, PostgreSQL mappings, the initial migration and isolated integration tests exist. Session authentication, Goal endpoints and planning features are not implemented.

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

## Run locally

Prerequisites: .NET 10 SDK, Node.js 24 LTS with npm, and Docker with Compose v2 or later. Run commands from the repository root unless noted.

Start PostgreSQL:

```bash
cp .env.example .env
# Edit .env and choose a local development password before starting.
docker compose up -d --wait postgres
docker compose ps
docker compose exec -T postgres pg_isready -U pathway -d pathway
```

PostgreSQL 18 binds only to `127.0.0.1:5432`, with database/user `pathway`. Data persists in the Compose-managed `pathway_postgres_data` named volume, mounted at `/var/lib/postgresql` (PostgreSQL 18 layout). Stop with `docker compose down`; the volume is retained. The API uses an explicitly configured connection string; migrations are never applied at startup. Changing the password in `.env` does not update credentials in an already initialized volume.

Build and run the API:

```bash
dotnet build backend/Pathway.slnx
# Configure ConnectionStrings__Pathway as described below before using persistence.
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

Build the frontend:

```bash
npm --prefix frontend run build
```

Tailwind and Vitest remain deferred. Never commit `.env` or real credentials.


## Explicit database migrations

EF Core 10 uses the Npgsql provider. Restore the pinned local EF tool from the repository root:

```bash
dotnet tool restore
```

Provide `ConnectionStrings__Pathway` in the process environment, or `ConnectionStrings:Pathway` through a private ASP.NET configuration source. The root `.env` is read by Compose, not automatically by the API. Connection string shape:

```text
Host=127.0.0.1;Port=5432;Database=pathway;Username=pathway;Password=<your-local-password>
```

With the development database started and that environment variable set, explicitly apply migrations:

```bash
dotnet ef database update --project backend/Pathway.Api
```

This command changes the database named by your configured connection string. The API never invokes `Migrate` or `EnsureCreated` on startup. Inspect SQL without applying it:

```bash
dotnet ef migrations script --project backend/Pathway.Api
```

Only sessions and goals are migrated in Phase 02. Entity timestamps must be supplied as UTC `DateTime` values; target dates use `DateOnly`. Database constraints are the final integrity boundary; friendly input validation and IANA timezone validation will be implemented in later phases.

## Isolated PostgreSQL integration tests

```bash
bash scripts/test-persistence.sh
```

The script starts **only** `compose.test.yaml`, under a unique Compose project, using PostgreSQL 18 with disposable tmpfs storage and a dynamically assigned localhost port. It does not load or merge the development Compose configuration and never mounts the development volume. Its fixed password is for the disposable local test service only.

The fixture requires the dedicated `PATHWAY_TEST_CONNECTION` variable (set by the script), checks the test database/role names, creates a GUID-named database, explicitly applies migrations, and drops only that database afterward. Tests run sequentially in one class and reset rows only in that generated database. Separate DbContext instances verify actual persisted round trips. The script removes the test container/storage on exit, including test failures. No development connection-string fallback exists; calling `dotnet test` without test configuration fails rather than skips these tests.

Additional runner arguments can be supplied, for example:

```bash
bash scripts/test-persistence.sh --logger 'console;verbosity=normal'
```

Docker and NuGet access are required. No database migration or cleanup command in the test runner targets the development database.
