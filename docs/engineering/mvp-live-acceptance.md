# Real Qwen MVP acceptance — 2026-10-31

**Latest extension:** [plan guidance](plan-guidance.md) adds explanations, key
points and unverified resource suggestions, with a newer live pass and 144 backend
tests. The results below document the earlier title/effort acceptance checkpoint.

## Problem, scope and contribution

The developer prioritized a working, demonstrable local MVP over further
architecture or model investigation. Existing evidence covered synthetic
providers and direct Python model requests, not a real browser-to-model journey.
The developer selected Qwen 3 4B as an integration candidate and installed Ollama;
Codex generated the live test mode, executed acceptance and wrote this record.
This is AI-generated implementation under developer direction, not independent
developer-led implementation. No commit or public deployment was made.

## Implementation and trade-offs

`scripts/test-live.sh` explicitly enables live mode in the existing isolated E2E
runner. It starts PostgreSQL from `compose.test.yaml` in a unique Compose project,
applies existing migrations, then starts the existing Kestrel Backend and Vue/Vite
on localhost HTTPS ports 7040 and 5173. Per-process settings point the actual
registered `OllamaPlanGenerator` at `http://127.0.0.1:11434/api/generate`, model
`qwen3:4b-instruct-2507-q4_K_M`. Live mode never starts the synthetic provider.
No development database, `.env`, model selection default or migration changed.
Prompt, UI language, timeout, scheduler and normal API contracts remain unchanged.

The Playwright configuration selects a separate live journey in live mode and
retains existing synthetic cases otherwise. The live case accepts variable model
task counts and a 90-second provider deadline rather than assuming fixture output
or using the HTTP smoke's five-second client timeout. A single browser worker
avoids parallel model requests. Trace/video/screenshots remain disabled.

An optional keep-running mode makes the accepted test environment manually usable.
It reuses existing localhost development TLS files when available; otherwise a
temporary certificate is generated. Only test resources owned by the runner are
cleaned up. Ollama and downloaded models are not stopped/deleted. Test data uses
tmpfs and is not durable storage. A private browser window avoids mixing cookies
with a pre-existing localhost development session.

Reusing the runner was smaller than adding a new application hosting stack. This
is a development/demo process, not production deployment infrastructure. Existing
unit and synthetic failure tests remain the deterministic regression baseline;
live-model acceptance is intentionally explicit and not part of every unit run.

## Actual browser and SQL acceptance

Command: `PATHWAY_E2E_KEEP_RUNNING=1 bash scripts/test-live.sh`.

**1 real Chromium case passed** (25.0 seconds including runner startup;
the test itself 24.2 seconds). Actual preview request: **20,008 ms**,
**4 milestones / 8 tasks**. No response was mocked.

Verified through the real Vue UI and Backend:

1. A new browser session creates a synthetic, clearly marked acceptance goal.
2. All three planning answers are entered and saved.
3. Backend calls local Qwen, validates the output and returns scheduled preview.
4. Direct PostgreSQL SQL verifies Draft status, three answers and zero saved
   milestones/tasks before confirmation.
5. Browser edits the first task's title and effort to two hours, then confirms.
6. Direct SQL verifies Active status, milestone/task counts, exact ordered titles,
   minutes (`hours * 60`) and start/end dates against the confirmation response.
7. Page reload retains the plan and edited next action; API reread matches the
   saved milestones exactly.
8. Completing a task and reloading retains its checked state.
9. An independent browser session sees an empty dashboard and gets 404 for the
   first session's goal; a POST without the CSRF header returns 400.

The automated browser context is isolated; a manual browser starts its own goals.
Only synthetic answers were used. Tokens, cookies and private answers are not
included in evidence output. This pass exercises the real C# adapter, unlike the
earlier Python-only model experiments.

## Regression and prior boundary work

- Full backend suite on a separate disposable PostgreSQL project:
  **126 passed, 0 failed, 0 skipped**. Its test container/network were removed.
- Frontend Node suite: **10 passed, 0 failed**.
- Vue TypeScript/production build passed; backend build: zero warnings/errors.
- Prior response-boundary unit work was retained: red run **27 failed / 12 passed**,
  then **75 database-independent tests passed**, including all 39 adapter cases.
  The minimal fix checks object envelopes and confirmed normal completion, and
  rejects unknown/case-mismatched nested plan fields via built-in JSON options.
  It introduces no package and does not alter global MVC JSON options.
- Synthetic upstream fixture now includes `done` and `done_reason`; its earlier
  four Chromium cases were not rerun in this milestone because the live demo
  occupies the same ports. The 126-test backend regression did run today.

Completion format was checked against the native
[Ollama API](https://docs.ollama.com/api/generate) and
[0.40.13 response type](https://github.com/ollama/ollama/blob/v0.40.13/api/types.go).
Two tiny direct probes of the selected model observed `true/stop` for normal
completion and `true/length` at a one-token limit. Missing/unknown reasons are
deliberately rejected rather than inferred as success; this can require explicit
compatibility review for a future provider/version. The scope is native
`/api/generate`, not every model/provider protocol.

## Limits and remaining work

- Local interactive MVP journey is accepted; the PRD's public deployed-app
  success criterion is still pending. No public host/domain was provisioned.
- Qwen 3 remains an integration candidate; this is one synthetic goal, not a
  general model-quality, accuracy, throughput or concurrency benchmark.
- Real model-unavailable/timeout UI failure scenarios were not run in this live
  pass. Existing adapter/HTTP failure tests remain; no real 90-second cancellation
  claim is made. Further nonblocking edge cases are deferred.
- Traditional Chinese prompt/UI were retained; English product migration is
  undecided. Recurring routines/waiting periods remain outside the scheduler.
- Frontend, Backend, PostgreSQL and Ollama coexisted successfully. A later idle
  snapshot showed about 4.7 GiB WSL RAM available and negligible swap, but the
  model had auto-unloaded; this is not peak inference memory evidence.
- Limiters remain process-local; retention cleanup and multi-user capacity remain
  deferred. Anonymous access still depends on the original browser cookie.
- Demo data disappears when its disposable environment stops. For public use,
  provide durable PostgreSQL storage/backups, persistent Data Protection keys,
  trusted HTTPS proxy configuration and privately reachable inference capacity.

Reproduce from the root using `bash scripts/test-live.sh` (automatic cleanup), or
`PATHWAY_E2E_KEEP_RUNNING=1 bash scripts/test-live.sh` (Ctrl+C cleans up). A local
model server and installed Chromium dependencies are prerequisites. See README
for setup and certificate trust; browser test certificate acceptance does not
change application TLS validation.
