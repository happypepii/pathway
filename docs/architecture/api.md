# REST API Contract — Proposed v0.1

Base path: `/api/v1`. JSON request/response bodies; UUID identifiers. This is a design contract, **not yet implemented**.

## Anonymous sessions

| Method | Path | Description |
|---|---|---|
| POST | `/sessions/anonymous` | Create or reuse valid anonymous session; set secure cookie |
| GET | `/sessions/me` | Check session state, not user identity |

Cookie: `__Host-pathway_session`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, no Domain. Same-origin HTTPS deployment is preferred. For local HTTP development, use a separate dev-only cookie policy; never weaken production defaults.

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
