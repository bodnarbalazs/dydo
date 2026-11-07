#!/usr/bin/env bash
set -euo pipefail
dotnet --version
dotnet --list-runtimes
dotnet tool list --local
node --version
pnpm --version
pwsh -NoLogo -NoProfile -Command '$PSVersionTable.PSVersion.ToString()'
.static-gates/python/bin/python --version
.static-gates/python/bin/python -m pip check
.static-gates/python/bin/python -m pip list --format=freeze
for dir in DynaDocs.Tests/coverage viewer skills/engineering/statechart/scripts; do
  test -d "$dir/node_modules"
  cmp "$dir/pnpm-lock.yaml" "$dir/node_modules/.pnpm/lock.yaml"
  pnpm -C "$dir" list --depth 0
done
node setup-skills.mjs
test -f DynaDocs.Tests/coverage/metrics/obj/project.assets.json
