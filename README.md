# Pathway

Turn overwhelming goals into actionable plans.

Vue 3 + TypeScript, ASP.NET Core .NET 10, EF Core/PostgreSQL 18. A single API handles anonymous sessions, Goal CRUD, three optional planning questions, editable plan previews, deterministic scheduling, atomic confirmation and task progress.

**Status:** MVP functionality implemented; 98 backend tests and four Chromium journey tests passed. Live LLM provider/model and public deployment acceptance remain pending. Missing provider configuration returns a safe 503. No paid service, live-model result or public deployment is claimed. See [implementation evidence](docs/engineering/mvp-implementation.md).

## Requirements

- WSL2/Linux, .NET 10 SDK, Node.js 24/npm.
- Docker Engine/Desktop with Compose and WSL integration.
- OpenSSL/Python 3 for the isolated browser harness.
- A configured planning provider. The current optional adapter supports Ollama's /api/generate protocol; no model is downloaded automatically.

## Local database and migrations

From repository root:

```bash
cp .env.example .env
# Set your own POSTGRES_PASSWORD in .env before starting.
docker compose up -d --wait postgres
dotnet tool restore
```

Provide the API connection string through a private configuration source or process environment:

```text
ConnectionStrings__Pathway=Host=127.0.0.1;Port=5432;Database=pathway;Username=pathway;Password=<your-local-password>
```

Compose reads .env; the API does not. Never commit .env, API keys, connection strings or certificate private keys. The development database is stored in pathway_postgres_data and bound only to 127.0.0.1:5432.

Inspect and explicitly apply migrations to the database you configured:

```bash
dotnet ef migrations script --project backend/Pathway.Api
dotnet ef database update --project backend/Pathway.Api
```

Startup never changes schema. The three migrations create sessions/goals, add timezone/concurrency fields, then create answers/milestones/tasks. The timezone migration uses UTC for any pre-existing goals. Scheduled expired-data cleanup is deferred.

## HTTPS development

Export a development-only localhost certificate in the ignored secrets directory:

```bash
mkdir -p frontend/secrets
dotnet dev-certs https --trust
dotnet dev-certs https --export-path frontend/secrets/localhost.pem --format Pem --no-password
```

The exported .key is unencrypted for local tooling only. Trust the public certificate in the browser/host OS as needed: WSL certificate trust may not propagate to Windows. Use localhost consistently.

Backend terminal, from repository root, with the private database connection configured:

```bash
export ASPNETCORE_Kestrel__Certificates__Default__Path="$PWD/frontend/secrets/localhost.pem"
export ASPNETCORE_Kestrel__Certificates__Default__KeyPath="$PWD/frontend/secrets/localhost.key"
# Set Planning__Endpoint / Planning__Model after choosing a provider/model (below).
dotnet run --project backend/Pathway.Api --launch-profile https
```

Frontend terminal:

```bash
npm --prefix frontend ci
export PATHWAY_DEV_CERT="$PWD/frontend/secrets/localhost.pem"
export PATHWAY_DEV_KEY="$PWD/frontend/secrets/localhost.key"
npm --prefix frontend run dev
```

Open **https://localhost:5173**. Vite proxies relative /api requests to https://localhost:7040 and validates the Backend certificate using the supplied public certificate. There is no CORS policy, no HTTP cookie fallback, and no TLS verification bypass in application configuration.

The frontend first calls GET /api/v1/sessions/me; only 401 triggers Antiforgery bootstrap and POST session creation. Cookies are HttpOnly, Secure and host-only. Session duration is fixed at 30 days. Clearing cookies or changing devices can lose access; no accounts/recovery exist.

## Planning provider configuration

Provider/model/budget is an open decision. No provider is called until the operator explicitly configures it. The optional Ollama protocol adapter accepts:

```text
Planning__Endpoint=http://127.0.0.1:11434/api/generate
Planning__Model=<an-existing-operator-selected-model>
```

Endpoint/model are server-only trusted configuration, never browser input. Use a local endpoint or HTTPS for remote sensitive data. No Ollama server or model is installed by this repository. Before enabling a remote provider, consider data transfer and any usage cost. Other provider protocols can implement IPlanGenerator without changing scheduling/persistence.

The adapter requests a structured schema, allows 90 seconds, limits output to 128 KiB and validates titles/counts/whole-hour estimates. It performs no automatic retry. Provider failure gives 503; malformed/oversized output gives 502, with no saved tasks.

## Manual acceptance

1. Create a goal with title, target date and weekly whole hours; timezone comes from the browser. Invalid input must show validation errors.
2. Edit the Draft; answer or skip the three optional questions.
3. With a real provider configured, generate a preview. Edit task titles/hours or remove tasks. No tasks are saved at this stage.
4. Confirm. The server revalidates, recalculates dates and atomically saves the plan. Infeasible dates show a warning without reducing effort.
5. Mark tasks complete; progress/next action change. Finish all tasks, then reopen one. Refresh and verify persistence.
6. Edit an activated goal's title/description; its scheduling constraints stay fixed. Delete the goal to remove its descendants.
7. Open an independent browser context: it must not see or mutate the first session's goals/tasks.
8. Inspect cookies and browser storage: session bearer token is only in the HttpOnly cookie, not JSON/localStorage/sessionStorage. POST without the Antiforgery pair returns 400.

Do not paste tokens, private answers or credentials into logs/reports.

## Automated verification

Full backend suite on a unique disposable real PostgreSQL instance:

```bash
bash scripts/test-persistence.sh
```

This uses only compose.test.yaml, a dedicated role/database, random host port and tmpfs storage. The fixture creates a GUID-named database, applies migrations explicitly and removes it after testing. No development database fallback exists. Calling dotnet test without database configuration fails the integration fixtures rather than silently skipping them.

Frontend unit checks and build:

```bash
node --experimental-strip-types --test --test-reporter=spec frontend/tests/*.test.ts
npm --prefix frontend run build
```

The dependency-free Node runner currently tests session bootstrap and the API client. Vitest remains deferred; Playwright provides UI integration coverage.

Browser suite prerequisites:

```bash
frontend/node_modules/.bin/playwright install chromium
# Linux/WSL: this may require your sudo password.
sudo "$(command -v node)" frontend/node_modules/playwright/cli.js install-deps chromium
bash scripts/test-e2e.sh
```

Leave ports 5173/7040 free. The harness starts isolated PostgreSQL, applies migrations, generates ephemeral localhost TLS, and runs real Chromium against Vue/HTTPS API/PostgreSQL. It uses an explicitly synthetic local HTTP upstream fixture through the actual provider adapter. This verifies integration and failure handling, **not live-model quality**. Test-only browser certificate acceptance does not weaken the app's Vite upstream TLS validation. Trace/video/screenshots are disabled to avoid capturing credentials/private input.

The explicit Node path in the sudo command supports nvm installations, where sudo's PATH may not contain `node`. Run it from your own terminal so sudo can request your password.

To verify the full HTTPS HTTP journey while browser libraries are unavailable:
```bash
PATHWAY_E2E_HTTP_ONLY=1 bash scripts/test-e2e.sh
```
This explicitly skips browser tests and must not be reported as a browser pass.

The runner terminates its own processes and removes its disposable container/network, including failures. It never uses development data.

## Single-origin local publish

```bash
bash scripts/publish-local.sh
# Configure database/Planning and an HTTPS certificate as above.
# Apply migrations explicitly before starting.
cd artifacts/publish
dotnet Pathway.Api.dll --urls https://localhost:7040
```

The publish script builds Vue and copies assets to API wwwroot. Kestrel serves frontend and /api under the same HTTPS origin; no Vite server is needed. Use an appropriate certificate/private configuration for public hosting. Host selection, trusted proxy headers, persistent Data Protection keys and operational backups remain deployment work; no public deployment is provisioned here.

## Constraints and trade-offs

- Sequential scheduling distributes capacity evenly across seven days; dates are estimates, not time-of-day reservations. Whole hours at the boundary, integer minutes in PostgreSQL.
- Activation is Draft-only. No replacement/regeneration of active plans; preview version mismatch/repeated confirmation returns 409.
- Session POST limit: 20 per connection IP per hour, including reuse and CSRF failures. GET-first initialization avoids consuming POST quota on normal refreshes.
- Preview limit: five per IP per minute. Limiters are process-local and reset on restart; NAT shares quota. Forwarded headers are not trusted automatically.
- Anonymous session possession is authorization, not verified identity. Every nested operation checks ownership; unknown/not-owned resources return 404.
- No DAG, accounts, notifications, cross-device sync, distributed infrastructure or automatic schema/data cleanup.

## Documentation

- [PRD](docs/product/prd.md), [user stories](docs/product/user-stories.md)
- [Architecture](docs/architecture/overview.md), [database](docs/architecture/database.md), [API](docs/architecture/api.md)
- [ADRs](docs/decisions/), [engineering evidence](docs/engineering/), [portfolio attribution](docs/portfolio.md)

Local commits are permitted for milestone tracking. Nothing is pushed automatically.
