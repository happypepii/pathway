# Local Ollama validation — 2026-10-31

**Latest status:** subsequent real Backend/Chromium/PostgreSQL acceptance passed;
see [MVP live acceptance](mvp-live-acceptance.md). The Python experiments below
remain preliminary model comparisons, not the evidence for that later pass.

## Scope and contribution

Developer selected `qwen2.5:1.5b` as the initial local integration model and
authorized small, individually confirmed steps. Public deployment is deferred.
The developer installed Ollama in WSL; Codex performed the queries and synthetic
inference experiments below. No developer-led model implementation or quality
improvement is claimed.

## Environment

- Ubuntu 24.04.4 LTS on WSL 2; WSL package 3.0.3.0.
- Intel i5-12500H; 16 logical processors visible; WSL RAM 7.6 GiB.
- RTX 3050 Laptop GPU, 4 GiB VRAM; NVIDIA driver 581.83.
- Ollama 0.40.13, systemd active, listening on 127.0.0.1:11434.
- Model ID `65ec06548149`, 986 MB, Q4_K_M, Apache 2.0.
- Backend development runs directly in WSL; Compose defines PostgreSQL only.

The local endpoint keeps Backend-to-Ollama networking within the same WSL
distribution. Windows installation was an alternative but would introduce
cross-system networking considerations. GPU visibility was confirmed using
`nvidia-smi`; later `ollama ps` reported 100% GPU at a 2,048-token context.

## Preliminary text experiments

The first short Chinese request took 49.817 seconds, including 23.168 seconds
loading, and returned 45 tokens. It completed but mixed in a simplified Chinese
character. Subsequent warm requests used an English question with a system
instruction selecting the language, seed 42, temperature 0.2, context 2,048 and
output limit 160 tokens. The Traditional Chinese request took 1.382 seconds but
returned English; the English request took 1.318 seconds and returned English.
Both completed normally. Single requests, different output lengths and prompt
cache/order effects prevent a conclusion that English is faster. These tests also
do not establish reliable Traditional Chinese instruction following.

## English structured-plan experiment

Problem: establish whether the real local model can return a candidate matching
Pathway's existing plan shape before changing application language or wiring a
live provider into the full HTTP journey.

Method: a temporary Python client extracted the JSON Schema and system prompt
from `OllamaPlanGenerator.cs`, replacing only the title-language instruction with
English. It used a synthetic beginner balcony-gardening goal with two hours of
morning sunlight, inexpensive supplies and an eight-week outcome. It sent
`stream=false`, temperature 0.2 and `num_predict=4096`, with no context override,
matching the adapter's current options. The client used a 90-second socket
timeout and checked elapsed time on completion; this is not proof of the C#
adapter's cancellation deadline. No tasks were written to a database.

Actual results:

- Wall time 5.232 seconds; model load 2.414 seconds; generation 2.695 seconds.
- 139 prompt tokens, 290 output tokens, 3,635-byte envelope, normal stop.
- 5 milestones, 6 tasks, 7 total estimated hours; English titles on inspection.
- Parsed envelope and inner JSON successfully. Python recursive checks against
  the extracted schema passed: types, required/extra fields, lengths, counts and
  integer ranges. Additional checks covered nonblank titles, UTF-16 title length,
  NUL rejection and the 100-task overall limit.

This did **not** execute `OllamaPlanGenerator`, `PlanningValidation` or the
Backend HTTP pipeline; equivalent input checks in an experimental client are
not an integration-test pass.

Quality limitations: most milestones had one task despite the prompt requesting
2–5; the hard contract allows one, so validation correctly accepted them.
The answer prescribed daily watering without accounting for individual plant
needs. It also represented eight weeks of care with one-time effort estimates;
the current sequential scheduler does not model recurring routines or elapsed
waiting periods. Schema conformity therefore does not establish plan quality.

## Next step and remaining acceptance

Application prompts and UI remain Traditional Chinese. The experiment alone is
not a decision to translate the product. Next, obtain developer confirmation for
the scope of English output, then test an actual Backend preview with the real
adapter and C# validation. Real timeout/model-unavailable handling and no-write
failure behavior still require live integration validation. Larger-model quality
comparison was subsequently requested by the developer; see the experiment below.

## Developer-requested Gemma 3 4B comparison

The developer requested testing Google Gemma 3 4B with English output and
measuring hardware behavior before selecting a production model. Codex downloaded
the official `gemma3:4b` tag (ID `68ceab7e468a`, 3.4 GB, Q4_K_M) and ran a
synthetic comparison. This model has completion and vision capabilities, but only
text completion was tested. It uses Gemma Terms of Use rather than Apache 2.0.
Sources: [Ollama model](https://ollama.com/library/gemma3:4b),
[Google model card](https://ai.google.dev/gemma/docs/core/model_card_3).

Method: reuse the exact English system prompt, synthetic goal, extracted schema,
temperature 0.2 and 4,096-token output cap from the Qwen experiment. No context
override was set; Ollama allocated a 4,096-token context. Both models were
unloaded from memory before the first Gemma generation, retaining downloaded
files. One model-reload request and one warm request were measured. This is not
a cleared filesystem/CUDA-cache cold-start benchmark. No seed was set, matching
the adapter and prior structured-plan experiment. The same Python schema and
additional plan checks were used; no C# application code was executed.

| Metric | Gemma model reload | Gemma warm |
|---|---:|---:|
| Wall seconds | 60.803 | 23.609 |
| Load seconds | 24.994 | 0.001 |
| Prompt processing seconds | 8.550 | 0.234 |
| Output tokens | 368 | 329 |
| Generation seconds | 27.248 | 23.295 |
| Milestones / tasks | 5 / 14 | 5 / 12 |
| Total estimated hours | 19 | 17 |

Both returned complete English JSON, passed the experiment's checks and finished
within 90 seconds. By comparison, the earlier single Qwen structured-plan request
took 5.232 seconds including 2.414 seconds loading, and returned 290 tokens in
2.695 seconds of generation (5 milestones / 6 tasks / 7 hours). These are small,
sequential samples with different output lengths and cache states, not a
statistically controlled performance or quality benchmark.

Hardware observations:

- `ollama ps`: Gemma **45%/55% CPU/GPU**, model allocation approximately 2.9 GB,
  context 4,096. The API reported 2,941,486,365 bytes allocated and 1,608,211,496
  bytes in VRAM. These allocation percentages are not GPU utilization percentages.
- Approximately one-second sampling during both requests: 79 samples; peak
  whole-GPU usage **3,583 MiB of 4,096 MiB**, peak sampled GPU utilization 50%.
  Whole-GPU memory includes Windows/other consumers, not just Ollama.
- WSL available RAM: baseline 5,463.9 MiB; lowest sampled 4,483.1 MiB. Swap use
  remained about 0.4 MiB. These are system-wide observations, not process memory
  attribution. No OOM occurred in these two requests; Backend/PostgreSQL were not
  running, so combined-application capacity remains unverified.
- Earlier Qwen short-text testing reported 100% GPU at context 2,048; no equivalent
  resource-sampled Qwen structured-plan benchmark was performed.

Qualitative inspection: both Gemma outputs obeyed the requested 2–5 tasks per
milestone. The warm output determined watering frequency from plant needs instead
of prescribing daily watering, improving on that specific Qwen sample. However,
it did not explicitly carry the two-hour sunlight/budget constraint through its
task titles, the 3-hour planting and 2-hour pot-preparation estimates appear coarse
for three plants, and recurring eight-week care remains incompatible with a purely
sequential effort schedule. Neither output establishes generally superior quality.

Conclusion: Gemma 3 4B English is feasible for this small text request on the
current hardware, but requires CPU offload under observed conditions and has a
substantial latency cost compared with the earlier Qwen sample. Keep the model
choice experimental until the developer selects the trade-off. Pathway prompts,
configuration, UI and database were unchanged; no timeout/model-unavailable or
vision acceptance is claimed from this comparison.

## Gemma removal and Qwen 3 4B comparison

The developer explicitly requested deleting Gemma before testing the next model.
Codex executed `ollama stop gemma3:4b` and `ollama rm gemma3:4b`; both succeeded.
The model list confirmed Gemma was absent and Qwen 2.5 1.5B remained. Historical
Gemma evidence above is retained. The official
[`qwen3:4b-instruct-2507-q4_K_M`](https://ollama.com/library/qwen3:4b-instruct-2507-q4_K_M)
was downloaded successfully: ID `0edcdef34593`, 2.5 GB, Q4_K_M, Apache 2.0.

The same temporary client, prompt, synthetic goal, schema, validation and hardware
sampling used for Gemma were reused, changing the model tag and result filename.
Qwen 2.5 and Qwen 3 were unloaded from memory before the first request. Context
was not overridden; Ollama allocated 4,096 tokens. No application settings or
database contents changed. No additional model tuning was performed.

| Metric | Qwen 3 model reload | Qwen 3 warm |
|---|---:|---:|
| Wall seconds | 38.747 | 15.232 |
| Load seconds | 16.480 | 0.001 |
| Prompt processing seconds | 8.750 | 0.053 |
| Output tokens | 289 | 310 |
| Generation seconds | 13.507 | 15.123 |
| Milestones / tasks | 4 / 8 | 4 / 8 |
| Total estimated hours | 17 | 21 |

Both returned complete English JSON, passed the experimental schema/additional
checks and completed within 90 seconds. In these samples, requests were shorter
than Gemma's 60.803/23.609 seconds. Output lengths and cache states differ; two
requests per model are not a controlled benchmark or proof of stable latency.

Hardware: `ollama ps` reported **18%/82% CPU/GPU** and approximately 2.9 GB model
allocation (2,874,041,957 bytes total, 2,344,636,907 bytes VRAM). Across 51 roughly
one-second samples, whole-GPU memory peaked at **3,481 MiB of 4,096 MiB**, and
sampled GPU utilization peaked at 44%. WSL available RAM was 5,446.7 MiB before
testing and at least 4,572.9 MiB during sampling; swap stayed about 0.4 MiB. These
are system-wide measurements, not solely model usage. Both Backend and PostgreSQL
were absent, and no OOM occurred. Joint application load remains unverified.

Qualitative observations: both outputs followed the requested milestone/task
counts. They included morning sunlight, low-maintenance plants, inexpensive
materials and plant-specific watering. The exact two-hour sunlight constraint
was not repeated. Unlike Gemma's samples, the eight-week outcome appeared
explicitly in the generated work. However, recurring observation was still
represented as one-time effort, and the warm task "Ensure all plants remain
healthy throughout the eight-week period" is an outcome rather than a concrete
action. Estimates of 17 versus 21 hours also show variability; titles/format alone
do not establish reliable estimates or general planning quality.

Qwen 3 is a reasonable next integration candidate given these specific results:
less observed CPU offload and lower latency than Gemma, with relevant constraints
represented. Qwen 2.5 remains the smaller, faster baseline. Model selection still
requires developer confirmation. Final model list: Qwen 2.5 1.5B and Qwen 3 4B;
Gemma is deleted. No live Backend acceptance, vision testing, public deployment
or Git commit is claimed.
