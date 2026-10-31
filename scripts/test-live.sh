#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Explicit opt-in: real local Qwen inference; all application data is disposable.
export PATHWAY_E2E_LIVE=1
unset PATHWAY_E2E_HTTP_ONLY
exec bash scripts/test-e2e.sh
