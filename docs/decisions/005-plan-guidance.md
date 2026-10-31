# ADR-005: Bounded plan guidance and unverified resource suggestions

**Status:** Accepted within the requested plan-content slice, 2026-10-31

## Context

The developer requested purpose/explanations, key points and learning resources
in addition to the complete visual path. The local model cannot verify websites.
Existing plans and sequential scheduling must remain usable, and generated
metadata must survive preview confirmation and reload.

## Decision

- Stage guidance: description/purpose up to 1,000 characters, expected outcome up
  to 500, and up to two resources. Task guidance: description up to 2,000 characters
  and up to three key points of 200 characters each.
- Resource fields are name (200), purpose (400) and searchQuery (200). Suggestions
  are explicitly unverified. No URL field exists; HTTP(S)/www URL text is rejected
  in resource fields. The browser renders plain text and constructs optional
  search links using a fixed origin and encoded query, never model-provided hrefs.
- New provider generations require nonempty guidance, key points and resources.
  Confirmation permits missing metadata for backward compatibility, but validates
  any supplied metadata. Existing ownership/version/transaction checks remain.
- Extend the existing task description column; add task key_points as text[], and
  nullable stage description/outcome plus a bounded resources_json JSONB array.
  Resources are display-only ordered metadata, not independently addressable
  entities; a separate table is unnecessary at present. Explicit serialization
  to a scalar JSONB-backed string keeps EF tracking simple without new converters
  or packages. Queryable/shared resources would justify revisiting this choice.
- The additive migration gives old rows empty arrays and nullable guidance; no
  original titles, effort, dates, progress or session ownership are rewritten.
- Scheduling carries metadata through but does not use it to assign dates.
  Prompt length/output count is kept small for the current 90-second local model
  budget; the timeout, selected model and deterministic algorithm are unchanged.

## Alternatives and trade-offs

Raw generated links risk misleading references. Live search adds an integration,
cost/latency and source-validation policy; it is deferred. A curated catalog could
later provide verified URLs. A normalized resource table adds joins/identities
without present independent-resource operations. Keeping metadata only in the
browser would lose it on confirmation or reload and is rejected.

Old plans cannot acquire explanations without generation; they display their
original path normally. Richer output consumes more tokens, so real-model browser
acceptance remains necessary. Suggested resource existence/relevance is not
guaranteed, even when structural validation passes.
