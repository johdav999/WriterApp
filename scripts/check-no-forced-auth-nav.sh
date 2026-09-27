#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "Checking for forced auth navigation regressions..."

patterns=(
  'NavigateTo\([^)]*\/\.auth\/login'
  'NavigateTo\([^)]*https?:\/\/[^)]*\/\.auth\/login'
  'window\.location(\.href)?\s*=\s*["'"'"'].*\/\.auth\/login'
)

search_matches() {
  local pattern="$1"

  if command -v rg >/dev/null 2>&1; then
    rg -n -S "$pattern" \
      --glob '!**/bin/**' \
      --glob '!**/obj/**' \
      --glob '!**/publish/**' \
      --glob '!**/.azure-publish/**' \
      --glob '!**/wwwroot/js/*.bundle.js' \
      --glob '!**/*.min.js' \
      .
  else
    grep -R -n -E "$pattern" \
      --exclude-dir=.git \
      --exclude-dir=.next \
      --exclude-dir=node_modules \
      --exclude-dir=bin \
      --exclude-dir=obj \
      --exclude-dir=out \
      --exclude-dir=publish \
      --exclude-dir=.azure-publish \
      --exclude='*.bundle.js' \
      --exclude='*.min.js' \
      .
  fi
}

for pattern in "${patterns[@]}"; do
  if search_matches "$pattern"; then
    echo
    echo "FAIL: found prohibited auth auto-navigation pattern: $pattern"
    exit 1
  fi
done

echo "PASS: no forced auth navigation patterns found."
