# REST API Contract — v0.1

Base path: `/api/v1`. JSON request/response bodies; UUID identifiers. Anonymous session, Goal, planning and task endpoints are implemented. A local real-Qwen Chromium/Backend/PostgreSQL journey is verified; see docs/engineering/mvp-live-acceptance.md. Public deployment remains pending.

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
  "availableHoursPerWeek": 10,
  "timeZoneId": "Asia/Taipei"
}
```

Validation: trim title before checking 1–200 characters; optional description maximum 2,000 characters; target date today or later in the user's IANA timezone; weekly hours integer 1–168. Use DateOnly / PostgreSQL DATE for target dates. timeZoneId is required, must be an IANA identifier (UTC accepted), and is stored with the goal. Invalid inputs return ValidationProblemDetails. Successful creation saves Draft status and returns 201 Created with a Location header.

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

Pagination is deferred. A repeated plan confirmation returns 409; confirmation does not replace an active plan.

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

## Implemented planning and progress contracts

GET /goals returns the current session's goals, newest first, with status, progress (rounded task-count percentage), nextTask and milestones. GET /goals/{id} also returns questions, answers, goalVersion and saved schedule summary. A different owner receives 404.

PATCH /goals/{id} accepts the same full form as creation. Drafts allow all changes; activated goals allow title/description edits only. Planning constraints remain immutable after activation. DELETE cascades to answers, milestones and tasks.

PUT /goals/{id}/planning-answers accepts:
```json
{ "experience": null, "outcome": "Maintain three healthy plants", "constraints": "Small balcony" }
```
All three answers are optional (2,000 characters each). The fixed questions are returned by goal detail. Answer editing requires Draft and returns 204.

POST /goals/{id}/plan-preview has no body. It uses stored answers, returns milestones, schedule and goalVersion, and saves no milestones/tasks. It is limited to five attempts per IP per minute. Unavailable/unconfigured/timeout provider returns 503; invalid or oversized provider response returns 502. Requests have no automatic provider retry.

The native Ollama adapter accepts an object envelope with `done: true`,
`done_reason: "stop"` and a string `response`. Missing/unknown completion reasons,
`length`, load/unload responses and non-object envelopes are safe output errors.
Inner plan property names are case-sensitive and unknown fields are rejected at
the plan, milestone and task levels, matching the generated schema. Other Ollama
envelope metadata is allowed. This strictness is adapter-local; confirmation's
handling of client-supplied dates is unchanged.

POST /goals/{id}/plan accepts:
```json
{
  "goalVersion": "<UUID from preview>",
  "milestones": [
    { "title": "Preparation", "tasks": [{ "title": "Gather materials", "estimatedHours": 2 }] }
  ]
}
```
Limits: 1–10 milestones, 1–100 tasks per milestone, 100 tasks overall, trimmed 1–200 character titles, whole-hour effort 1–1,000 per task. Preview/confirmation request limit is 128 KiB; provider response limit is 128 KiB with bounded JSON depth. Unknown dates from clients are ignored.

Confirmation locks the owned goal, verifies Draft and version, revalidates and recomputes dates using the current local date, saves atomically and returns updated goal detail. Stale/repeated/concurrent confirmation returns 409. Infeasible plans may be explicitly confirmed; their schedule warnings remain visible. Activation/regeneration is not reversible through this endpoint.

PATCH /tasks/{id}/completion accepts a required boolean completed and returns 204. Reopening is supported. Parent goal becomes Completed only when all tasks are complete; reopening makes it Active. PATCH /tasks/{id} accepts a trimmed title (1–200 characters) to rename a task; effort/date edits after activation are deferred. Every task query checks ownership through milestone → goal.

Concurrent modifications return 409 and require refresh/retry. GET detail and list responses use no-store. Frontend writes use the Antiforgery cookie/header pair described above.

## Plan guidance (US-08)

Preview, confirmation and saved detail/list milestone objects now include optional
`description`, `outcome` and `resources`. Task objects include `description` and
`keyPoints`. Confirmation accepts missing metadata for old clients/plans, validates
any supplied values and stores the metadata in the same activation transaction.
New provider output requires these fields, with at least one resource and key
point; missing guidance yields the existing safe 502.

| Field | Limit |
|---|---|
| milestone.description | 1–1,000 characters when supplied |
| milestone.outcome | 1–500 characters when supplied |
| milestone.resources | at most 2 objects with name/purpose/searchQuery |
| resource.name / purpose / searchQuery | 200 / 400 / 200 characters, nonempty |
| task.description | 1–2,000 characters when supplied |
| task.keyPoints | at most 3 nonempty strings, each at most 200 characters |

Resource fields reject HTTP(S)/www URL text and unknown provider fields. UI shows
plain names/purposes plus an encoded search link to a fixed search-engine origin,
explicitly marked AI-generated and unverified. No provider/browser-supplied URL is
used as an href. No web search or source verification occurs during generation.
Old saved plans return null/empty metadata and retain their original path.
