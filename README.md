# Pathway

Turn overwhelming goals into actionable plans.

Vue 3 + TypeScript, ASP.NET Core .NET 10, EF Core/PostgreSQL 18. A single API handles anonymous sessions, Goal CRUD, three optional planning questions, editable plan previews, deterministic scheduling, atomic confirmation and task progress.

Preview and saved plans also show a connected visual path with every small task,
dates, whole-hour effort and completion/next-action state. Select a task node to
focus its editor or completion checkbox. Stages scroll horizontally on desktop
and stack vertically on mobile. This visualizes the existing sequence; it does
not introduce task dependencies. See [path verification](docs/engineering/visual-path.md).

**Status:** Local MVP checkpoint: real Qwen 3, visual paths, plan guidance and task-help prompt export. Backend regression: 144 passed; frontend unit tests: 14 passed; synthetic/live-mode browser suites: 9/6 passed. A reported goal-alignment failure remains unresolved: background details can displace the user's primary goal despite valid output structure. Public deployment is pending. Work is paused until the developer returns. See [checkpoint and follow-up](docs/engineering/mvp-closeout.md).

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

Startup never changes schema. The four migrations create sessions/goals, add timezone/concurrency fields, create answers/milestones/tasks, then add guidance metadata. The timezone migration uses UTC for any pre-existing goals; the guidance migration preserves rows with nullable/empty metadata. Scheduled expired-data cleanup is deferred.

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

The current local integration candidate is Qwen 3 4B Instruct; Qwen 2.5 1.5B remains a lightweight baseline. This is not a final product-model decision. No provider is called until the operator explicitly configures it. The Ollama protocol adapter accepts:

```text
Planning__Endpoint=http://127.0.0.1:11434/api/generate
Planning__Model=qwen3:4b-instruct-2507-q4_K_M
```

Endpoint/model are server-only trusted configuration, never browser input. Use a local endpoint or HTTPS for remote sensitive data. No Ollama server or model is installed by this repository. Before enabling a remote provider, consider data transfer and any usage cost. Other provider protocols can implement IPlanGenerator without changing scheduling/persistence.

The adapter requests a structured schema, allows 90 seconds, limits output to 128 KiB and validates titles/counts/whole-hour estimates. It performs no automatic retry. Provider failure gives 503; malformed/oversized output gives 502, with no saved tasks.

New plans include stage purpose/outcome, task descriptions/key points and up to
two resource suggestions per stage. Resource names, purposes and search keywords
are AI suggestions, **not verified references**. The UI provides a search link
constructed from the keywords rather than opening model-generated URLs. Existing
plans remain usable but do not automatically acquire guidance; create a new goal
to try the richer generation flow. See [ADR-005](docs/decisions/005-plan-guidance.md).

If a task is still hard to start, expand **這一步怎麼開始？** in its preview or
saved task. Review the generated context prompt, optionally include goal notes or
planning answers (both excluded by default), describe the obstacle and edit the
text before copying it to an external LLM. No API request or external transmission
is made by the feature. The draft stays in page memory; an explicit copy writes
to the clipboard, with manual text selection if clipboard permission is denied.
No subgoal is created and external answers are not automatically imported.

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

## Real Qwen acceptance and local demo

With Ollama running locally and the selected model already downloaded:

```bash
# Real Chromium → Vue → Backend → Ollama, with direct PostgreSQL checks.
bash scripts/test-live.sh

# Run acceptance, then keep the isolated environment available for manual use.
PATHWAY_E2E_KEEP_RUNNING=1 bash scripts/test-live.sh
```

This explicitly uses `http://127.0.0.1:11434/api/generate` and
`qwen3:4b-instruct-2507-q4_K_M`, with no synthetic upstream or provider fallback.
It creates a unique test PostgreSQL project, applies existing migrations and
starts the existing HTTPS Backend and Vite on ports 7040/5173. Only that Backend
process receives the test connection string and planning configuration. Existing
development data and `.env` are untouched. The live browser case verifies saved
answers, unsaved preview, edited confirmation, SQL rows/dates/minutes, reload,
completion persistence, session isolation and missing-CSRF rejection.

After the keep-running message, open **https://localhost:5173 in a private browser
window** to avoid mixing the demo cookie with an existing localhost development
session. The automated browser uses a separate session, so start a new goal in
your manual browser. Prompts/UI retain the current Traditional Chinese behavior.
The runner reuses `frontend/secrets/localhost.pem` and `.key` if present, otherwise
generates a temporary certificate. Your browser must trust the public certificate;
the automated browser's temporary-certificate acceptance is test-only.

The demo database is disposable tmpfs, not durable development storage. Ctrl+C in
the terminal running the command stops its services and removes its test database.
Normal mode also cleans up on completion/failure. No downloaded model is deleted.

To run a newer isolated demo while preserving an already-running 5173 environment:

```bash
PATHWAY_E2E_FRONTEND_PORT=5174 PATHWAY_E2E_BACKEND_PORT=7041 \
  PATHWAY_E2E_KEEP_RUNNING=1 bash scripts/test-live.sh
```

Open **https://localhost:5174** using another browser or an independent browser
profile if preserving an existing demo's anonymous session. Localhost cookies are
shared across ports; changing ports isolates service/data resources, not cookies.
This creates a new disposable database and applies migrations there, leaving the
old environment/database untouched. For manually started Vite, matching overrides
are `PATHWAY_DEV_PORT` and `PATHWAY_API_PORT` (defaults 5173/7040).

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

Git commits require explicit user approval. Nothing is pushed automatically.
