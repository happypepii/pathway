# Anonymous session bootstrap — 2026-10-30

## Problem and baseline

US-01 requires a draft goal owned by a browser session. Before this milestone, GET /api/v1/sessions/me could resolve a pre-existing cookie, but the browser could not obtain one. Persistence and fixed 30-day expiry already existed. The frontend was a static placeholder.

## Decision and alternatives

Implement POST /sessions/anonymous and GET /antiforgery with ASP.NET Core Antiforgery and a global MVC AutoValidateAntiforgeryToken filter. AddControllersWithViews supplies the built-in filter services; no views are mapped. An initial test caught missing filter registration with AddControllers; the registration was corrected and tests rerun.

Retain the accepted endpoint-wide 20 POST requests per IP per one-hour fixed window. Valid reuse and failed CSRF requests count as attempts. Exempting reuse or counting only successful creations would change the existing endpoint-wide contract and was not adopted. Instead the frontend calls GET /sessions/me first and only bootstraps on 401. A valid session is read-only and does not reissue the cookie or extend expiry.

Use HTTPS locally with the same Secure cookies as Production. Vite proxies relative /api URLs to HTTPS Kestrel and validates its certificate. This avoids a cross-origin cookie/CORS configuration and does not relax Production security. The local setup shares one trusted localhost certificate. Browser trust in a Windows/WSL setup requires host-specific installation.

No schema change, new dependency, repository abstraction or deployment infrastructure is added.

## Implementation and contribution

The developer selected the project direction, approved the milestone and security mechanisms, and requested explicit rate-limit/local-development review. Codex generated implementation, tests and this evidence under those constraints. Developer code review and independent verification remain pending; this is not evidence of developer-led implementation.

Changed code:
- backend/Pathway.Api/Program.cs: global Antiforgery filter, secure Antiforgery cookie, per-IP fixed-window limiter.
- Controllers/AntiforgeryController.cs: no-store request token issuance.
- Controllers/SessionsController.cs: resolve/reuse or create/hash/persist/issue cookie.
- frontend/src/session.ts and App.vue: GET-first bootstrap, same-origin requests, safe failure/retry UI.
- frontend/vite.config.ts: HTTPS dev certificate requirement and verified HTTPS proxy.

## Verification and actual results

Executed on 2026-10-30:
- Backend build: success, no warnings/errors.
- Filtered dotnet test for SessionSecurityTests, SessionTokenTests, AnonymousSessionPolicyTests and AnonymousSessionInputTests: **19 passed, 0 failed**. Includes six HTTP security cases covering CSRF rejection, mismatched pairs, quota exhaustion, GET independence, IP isolation, IPv4-mapped IPv6 normalization and ignoring untrusted X-Forwarded-For.
- Node 24 built-in runner, frontend/tests/session.test.ts: **5 passed, 0 failed**. Covers GET-only reuse, initial bootstrap sequence/header/credentials, no creation on non-401 errors, no automatic rate-limit retry and no POST on CSRF bootstrap failure.
- npm --prefix frontend run build: passed.
- Temporary HTTPS smoke experiment: generated a one-day localhost certificate in a temporary directory; started Production Kestrel and Vite using it; Python HTTPS client trusted only that certificate. GET through Vite /api/v1/antiforgery returned 200/no-store/Secure HttpOnly Strict cookie; POST without CSRF returned 400. Vite upstream certificate verification remained enabled. Processes were terminated and temporary certificate/private key removed.
- Full backend/PostgreSQL suite after Docker restoration: bash scripts/test-persistence.sh — **61 passed, 0 failed, 0 skipped**. Real PostgreSQL tests verified first-create, persisted hash, exact expiry/cookie expiry, reuse/no-write, invalid/expired replacement, CSRF/no-write and creation/reuse quota assertions. The disposable container and network were removed successfully. Sandbox Docker socket access required escalation.
- git diff --check: passed.

Earlier full-suite attempts were blocked by unavailable Docker in WSL; after the developer restored Docker, the full suite passed as recorded above. This is a resolved environment limitation.

Not verified:
- Browser-level cookie acceptance, host OS certificate trust and full frontend-to-database journey were not tested. The HTTPS smoke client is not a browser.
- Real-hour limiter replenishment was not timed; configured fixed-window behavior is delegated to the framework.

## Reproduce

1. Enable Docker Desktop WSL integration (or provide Docker Engine and Compose).
2. Run bash scripts/test-persistence.sh; the existing isolated disposable PostgreSQL fixture is used, with no development database fallback.
3. Run the filtered backend tests and frontend Node command documented in README.
4. Follow README HTTPS setup and browser smoke checklist.

## Limitations and next work

Process-local limits reset on restart and do not coordinate across instances. NAT shares a quota. Trusted reverse proxy/forwarded-header configuration must be designed for the selected deployment; no client-supplied forwarding header is trusted by this code. Production Data Protection key persistence also needs deployment configuration to avoid invalidating Antiforgery tokens after restarts. Expired-session cleanup remains deferred. Anonymous access is a bearer session, not verified user identity.

The milestone implementation and automated backend/persistence verification are complete; developer review and browser-level verification remain pending. No commit has been created for this implementation. After completing those checks and review, the next proposed milestone is US-01 draft goal creation, including the IANA timezone request contract, validation and ownership isolation.

## Reference documentation

- [ASP.NET Core Antiforgery](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0): built-in MVC validation and header-based request tokens.
- [Vite server options](https://vite.dev/config/server-options): HTTPS server and API proxy configuration.
