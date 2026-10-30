# Pathway

**Turn overwhelming goals into actionable plans.**

Pathway is an AI-assisted personal goal planning application. Users describe a goal, answer a few questions, review a generated task breakdown, receive a capacity-aware schedule, and track progress.

## Status

**MVP v0.1 — Phase 02 persistence.** EF Core entities, PostgreSQL mappings, the initial migration and isolated integration tests exist. Read-only session checking is available at `GET /api/v1/sessions/me`; session creation with CSRF protection and IP rate limiting is implemented; Goal endpoints and planning features are not implemented.

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

The development API listens on `http://localhost:5040`. `GET /api/v1/sessions/me` checks the secure session cookie; `/` returns 404. Session issuance requires HTTPS and an Antiforgery cookie/header pair. The optional `https` launch profile also listens on `https://localhost:7040` and requires a trusted development certificate. Production retains HTTPS redirection; development HTTP is scaffolding only, not the production cookie policy.

Run the frontend in another terminal:

```bash
cd frontend
npm ci
npm run dev -- --host 127.0.0.1
```

Open the URL printed by Vite (normally `http://localhost:5173`). The frontend checks the current session and creates one only on 401. For session-enabled development, use the HTTPS setup below; the dev server requires certificate paths.

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

## HTTPS session development

Export the ASP.NET localhost development certificate to an ignored directory (unencrypted private key: local development only):

```bash
mkdir -p frontend/secrets
dotnet dev-certs https --trust
dotnet dev-certs https --export-path frontend/secrets/localhost.pem --format Pem --no-password
```

Trust the public certificate in the browser/host OS too if WSL certificate trust does not propagate. Never commit the private key. Configure the usual private database connection string and apply migrations first.

Backend terminal, from repository root:

```bash
export ASPNETCORE_Kestrel__Certificates__Default__Path="$PWD/frontend/secrets/localhost.pem"
export ASPNETCORE_Kestrel__Certificates__Default__KeyPath="$PWD/frontend/secrets/localhost.key"
dotnet run --project backend/Pathway.Api --launch-profile https
```

Frontend terminal, from repository root:

```bash
export PATHWAY_DEV_CERT="$PWD/frontend/secrets/localhost.pem"
export PATHWAY_DEV_KEY="$PWD/frontend/secrets/localhost.key"
npm --prefix frontend run dev
```

Visit **https://localhost:5173**. Use localhost consistently. Vite validates the Backend certificate using this public certificate as its CA; do not disable verification. The browser uses relative /api URLs through the HTTPS proxy, with no cross-origin requests or CORS configuration. Secure cookies remain mandatory in Development and Production. The HTTP launch profile remains useful for unrelated scaffolding, not session bootstrap.

Browser smoke check: a first visit should call GET sessions/me (401), GET antiforgery (200), POST sessions/anonymous (201). Refresh should call only GET sessions/me (200). Inspect cookie attributes and confirm neither the session token nor CSRF request token appears in local/session storage. Direct POST without the CSRF pair returns 400; after 20 POST attempts from one IP, further POSTs return 429 while GET remains available. Do not copy tokens into tickets or logs.

Frontend bootstrap tests (Node 24 built-in runner; Vitest remains deferred):

```bash
node --experimental-strip-types --test --test-reporter=spec frontend/tests/session.test.ts
```

Database-independent backend checks:

```bash
dotnet test backend/Pathway.slnx --filter 'FullyQualifiedName~SessionSecurityTests|FullyQualifiedName~SessionTokenTests|FullyQualifiedName~AnonymousSessionPolicyTests|FullyQualifiedName~AnonymousSessionInputTests'
```

Run the full PostgreSQL suite using bash scripts/test-persistence.sh. See docs/engineering/session-bootstrap.md for this milestone's actual verification status and deployment limitations.
