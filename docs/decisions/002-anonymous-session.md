# ADR-002: Anonymous Sessions with PostgreSQL

**Status:** Accepted for MVP

## Context

Users should be able to try planning without signup, but goals must persist across page refreshes and remain isolated between visitors.

## Decision

Generate a cryptographically random opaque bearer token. Store only its SHA-256 hash in `anonymous_sessions`; send the token as a Secure, HttpOnly, SameSite=Lax, host-only cookie over HTTPS. Bind goals to session IDs. Enforce ownership checks in every API use case. Apply CSRF protection, rate limits, session expiry and data cleanup.

## Alternatives

Browser-only localStorage; full user authentication.

## Consequences

Lower friction and server-side persistence, but no reliable cross-device recovery. Losing the cookie can mean losing access. Session theft still grants access until expiry; token hashing does not replace cookie security. Fixed 30-day expiry is a proposal subject to confirmation.
