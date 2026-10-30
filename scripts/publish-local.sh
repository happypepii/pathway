#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
npm --prefix frontend ci
npm --prefix frontend run build
dotnet publish backend/Pathway.Api -c Release -o artifacts/publish
mkdir -p artifacts/publish/wwwroot
cp -R frontend/dist/. artifacts/publish/wwwroot/
echo "Published to artifacts/publish; configure database, HTTPS certificate and Planning before running."
