#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
project="pathway-e2e-$(cat /proc/sys/kernel/random/uuid)"
cleanup() { docker compose -p "$project" -f compose.test.yaml down --volumes; }
trap cleanup EXIT
docker compose -p "$project" -f compose.test.yaml up -d --wait postgres-test
binding=$(docker compose -p "$project" -f compose.test.yaml port postgres-test 5432)
port=${binding##*:}
export ConnectionStrings__Pathway="Host=127.0.0.1;Port=$port;Database=pathway_tests;Username=pathway_tests;Password=local-test-only;Include Error Detail=false"
export PATHWAY_E2E_PROJECT="$project"
dotnet build backend/Pathway.slnx
dotnet ef database update --project backend/Pathway.Api --no-build
python3 scripts/run-e2e.py
