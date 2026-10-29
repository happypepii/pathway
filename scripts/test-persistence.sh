#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# A unique Compose project prevents collisions between test runs.
project="pathway-tests-$(cat /proc/sys/kernel/random/uuid)"
cleanup() {
  docker compose -p "$project" -f compose.test.yaml down --volumes
}
trap cleanup EXIT
docker compose -p "$project" -f compose.test.yaml up -d --wait postgres-test
binding=$(docker compose -p "$project" -f compose.test.yaml port postgres-test 5432)
port=${binding##*:}
export PATHWAY_TEST_CONNECTION="Host=127.0.0.1;Port=$port;Database=pathway_tests;Username=pathway_tests;Password=local-test-only;Include Error Detail=false"
dotnet test backend/Pathway.slnx "$@"
