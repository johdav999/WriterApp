#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if ! command -v rg >/dev/null 2>&1; then
  echo "ERROR: ripgrep (rg) is required for this guardrail."
  exit 2
fi

echo "Checking for forced auth navigation regressions..."

patterns=(
  'NavigateTo\([^)]*\/\.auth\/login'
  'NavigateTo\([^)]*https?:\/\/[^)]*\/\.auth\/login'
  'window\.location(\.href)?\s*=\s*["'"'"'].*\/\.auth\/login'
)

for pattern in "${patterns[@]}"; do
  if rg -n -S "$pattern" \
    --glob '!**/bin/**' \
    --glob '!**/obj/**' \
    --glob '!**/publish/**' \
    --glob '!**/.azure-publish/**' \
    --glob '!**/wwwroot/js/*.bundle.js' \
    --glob '!**/*.min.js' \
    .; then
    echo
    echo "FAIL: found prohibited auth auto-navigation pattern: $pattern"
    exit 1
  fi
done

echo "PASS: no forced auth navigation patterns found."
