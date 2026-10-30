# REST API Contract — Proposed v0.1

Base path: `/api/v1`. JSON request/response bodies; UUID identifiers. This is a design contract; anonymous session and Antiforgery endpoints are implemented; Goal and planning endpoints remain proposed.

## Anonymous sessions

| Method | Path | Description |
|---|---|---|
| GET | `/antiforgery` | Issue Antiforgery cookie and request token |
| POST | `/sessions/anonymous` | Create or reuse valid anonymous session; set secure cookie |
| GET | `/sessions/me` | Check session state, not user identity |

Cookie: `__Host-pathway_session`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no Domain. Same-origin HTTPS deployment is preferred. Local development also uses HTTPS and the same Secure cookie attributes; no HTTP cookie fallback is provided.

### Implemented session check

`GET /sessions/me` reads `__Host-pathway_session` and returns `200 OK` with an empty body for a valid session. Missing, malformed, unknown or expired tokens return `401 Unauthorized`. Responses use `Cache-Control: no-store`; no credentials or session identifiers are returned. The endpoint does not create sessions, update last-seen or expiry, or set cookies.

## Goals

| Method | Path | Description |
|---|---|---|
| POST | `/goals` | Create draft goal |
| GET | `/goals` | List current session's goals |
| GET | `/goals/{goalId}` | Goal detail, milestones and tasks |
| PATCH | `/goals/{goalId}` | Update owned goal |
| DELETE | `/goals/{goalId}` | Delete owned goal and descendants |

Example POST `/goals`:

```json
{
  "title": "Build a personal website",
  "description": "Portfolio and deployment",
  "targetDate": "2026-11-30",
  "availableHoursPerWeek": 10
}
```

Validation: trim title before checking 1–200 characters; optional description maximum 2,000 characters; target date today or later in the user's IANA timezone; weekly hours integer 1–168. Use DateOnly / PostgreSQL DATE for target dates. The timezone request contract remains to be defined before implementation. Invalid inputs return ValidationProblemDetails. Successful creation saves Draft status and returns 201 Created with a Location header.

## Planning

| Method | Path | Description |
|---|---|---|
| PUT | `/goals/{goalId}/planning-answers` | Save/replace answers |
| POST | `/goals/{goalId}/plan-preview` | Generate and schedule unsaved preview |
| POST | `/goals/{goalId}/plan` | Validate edited preview, recompute dates and persist atomically |

Illustrative plan-preview response:

```json
{
  "milestones": [
    {
      "title": "Backend",
      "tasks": [
        {
          "title": "Build Goal API",
          "estimatedHours": 4,
          "scheduledStartDate": "2026-11-02",
          "scheduledEndDate": "2026-11-03"
        }
      ]
    }
  ],
  "schedule": {
    "totalEstimatedHours": 4,
    "isFeasible": true,
    "warnings": []
  }
}
```

Preview dates are computed by the server. The confirmation request contains editable milestone/task titles and `estimatedHours`, **not trusted scheduled dates**. Confirmation revalidates and recalculates before saving.

## Tasks

| Method | Path | Description |
|---|---|---|
| PATCH | `/tasks/{taskId}` | Update task fields with ownership check |
| PATCH | `/tasks/{taskId}/completion` | Set completed state; allow reopening |

Example completion body: `{ "completed": true }`.

## Error and security behavior

- `400` invalid request, `401` missing/invalid/expired session, `404` resource not found or not owned (avoid existence leaks), `409` invalid state/conflict, `429` rate limited, `502/503` upstream AI failure.
- All state-changing cookie-authenticated endpoints require CSRF defenses (ASP.NET Core antiforgery protection).
- Require ownership on every nested resource access.
- Limit generated tasks, request body size, and external API usage; configure timeouts and rate limiting.
- Never expose provider credentials or raw session tokens to browser JS.

## Open contract questions

Exact preview edit DTO shape, pagination, idempotency for plan confirmation, and provider-specific error taxonomy are deferred to implementation design.

## Approved session policy

- Fixed expiration 30 days after creation; no sliding expiration. Reject expired sessions immediately.
- Rate limit anonymous session creation to 20 requests per IP per hour initially.
- Scheduled cleanup is deferred.
- Raw session tokens appear only in the HttpOnly session cookie, never response bodies, logs or browser-readable storage.

## Implemented session bootstrap

1. Call `GET /sessions/me` with same-origin credentials. A valid session needs no POST.
2. On 401 only, call `GET /antiforgery`. It returns `200 { "requestToken": "..." }` and an HttpOnly, Secure, SameSite=Strict, Path=/, host-only `__Host-pathway_antiforgery` cookie. Response is no-store.
3. POST `/sessions/anonymous` with that cookie and the request token in `X-CSRF-TOKEN`. No body is required.
4. Missing/malformed/unknown/expired session cookies cause creation: 201 with empty body and a new session cookie expiring at the persisted creation time + 30 days. Only its hash is stored. Expired rows are retained.
5. A valid session returns 200 with empty body, without writes, last-seen changes, expiry extension or Set-Cookie.

The request token is an Antiforgery credential, not the anonymous session bearer token. Keep it in memory; do not log it or persist it in browser storage. Missing, invalid or mismatched Antiforgery cookie/header pairs return 400 before the action executes. MVC globally validates unsafe controller requests, including session bootstrap. GET/HEAD/OPTIONS/TRACE are excluded by the built-in filter.

### Rate limiting semantics

The existing endpoint-wide rule is preserved: **all POST /sessions/anonymous attempts**, including reuse and CSRF failures, count toward 20 requests per connection IP per one-hour fixed window. Request 21 returns 429 with no queued request or database write. GET /sessions/me and GET /antiforgery do not consume this quota. The frontend checks GET /sessions/me first, so normal valid-session page loads do not use POST quota; it does not automatically retry failures.

Windows begin when a partition is first used; this is not a sliding window. The limiter is process-local and resets on restart. Clients sharing NAT share quota. IPv4 and IPv4-mapped IPv6 addresses share a partition; absent IP uses one shared fallback. Client-supplied forwarding headers are not trusted. Before deployment behind a reverse proxy, explicitly configure trusted proxies and forwarded headers; otherwise the proxy's connection IP is the rate-limit key. Distributed limiting and deployment proxy configuration remain deferred.

### Local origin

Open only https://localhost:5173. Vite serves HTTPS and proxies relative /api requests to https://localhost:7040 with certificate verification enabled. No CORS policy is enabled. Both cookies are issued for the browser-visible localhost host with no Domain and Path=/; browser requests and CSRF header remain same-origin. Use the same trusted localhost certificate for Vite and Kestrel (README setup). No secure=false or relaxed Production cookie policy is introduced.
