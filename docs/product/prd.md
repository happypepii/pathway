# Pathway — Product Requirements Document

**Version:** 0.1  
**Status:** Proposed / not implemented  
**Target:** 3-week MVP, week 4 as buffer

## Vision

**From uncertainty to action.** Help people turn intimidating or ambiguous personal goals into small, actionable steps with realistic schedules.

## Problem

Users know what they want to achieve but struggle to determine where to begin, how to break work down and how to allocate limited time. A large checklist or unrealistic deadline may make the experience worse.

## Audience

People managing personal learning, career, fitness or project goals. The product is domain-general; it is not restricted to software learning.

## Core journey

1. Enter a goal, target date and available hours per week.
2. Answer 2–3 relevant guided questions (optional answers may be skipped).
3. Generate a milestone/task plan via an LLM.
4. Backend validates the structured output and computes a schedule.
5. Review/edit the preview; confirm before persistence.
6. Track completion and see the next action.

## MVP capabilities

- Anonymous browser session with server-side ownership checks.
- Create/list/view/edit/delete goals.
- Capture planning answers.
- AI-assisted milestone and task breakdown with effort estimates.
- Deterministic sequential scheduling based on available weekly hours.
- Editable plan preview; explicit confirmation.
- Dashboard with goal progress and next unfinished task.
- Validation, failure states and basic automated tests.

## Scheduling assumptions (MVP)

- User enters **whole hours**; database stores effort in integer minutes.
- Weekly capacity is evenly distributed across seven days as an *estimate* (not a promise of actual daily availability).
- Tasks may span multiple calendar days; dates represent planned allocation, not time-of-day reservations.
- Flag plans that cannot finish by the target date; do not silently shorten estimated effort.
- No task dependencies in MVP. DAG support is a stretch goal.

## UX principles

- Emphasize the next small action rather than an overwhelming wall of tasks.
- Explain unrealistic deadlines neutrally and offer edits.
- Let users review AI suggestions; do not treat AI output as authoritative.
- Make anonymous-session limitations clear: clearing cookies or switching devices may lose access.

## Out of scope

Accounts, cross-device sync, notifications, calendar integrations, social features, gamification, full chatbot, complex dependency DAG, distributed infrastructure.

## Success criteria

A new visitor can complete the full journey on a deployed app; another anonymous session cannot access their data; scheduling has meaningful unit tests; AI failures are handled without corrupting saved plans.

## Open decisions

- LLM provider and request budget.
- Precise session retention and deletion policy (initial proposal: fixed 30-day expiration).
- Deployment host and HTTPS/same-origin reverse proxy.
- Whether to permit plan regeneration after activation.
