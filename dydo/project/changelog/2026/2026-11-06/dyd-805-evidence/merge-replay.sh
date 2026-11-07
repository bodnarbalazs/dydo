#!/usr/bin/env bash
set -uo pipefail
root=/home/balazs/.t3/worktrees/DynaDocs/t3-c2271780
cd "$root" || exit 1
gate() {
  local name=$1 memory=$2 seconds=$3
  shift 3
  /var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice \
    -p "MemoryMax=$memory" -p MemorySwapMax=0 -p OOMPolicy=continue timeout "$seconds" \
    env PATH=/tmp/dyd622-pwsh:/home/balazs/.local/share/fnm/aliases/default/bin:/home/balazs/.dotnet:/usr/local/bin:/usr/bin:/bin \
    MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false \
    "$@" > "/tmp/dyd805-merge-$name.log" 2>&1
  local rc=$?
  printf '%s\t%s\n' "$name" "$rc" | tee -a /tmp/dyd805-merge-exits.tsv
  return "$rc"
}
: > /tmp/dyd805-merge-exits.tsv
light='FullyQualifiedName~Guard|FullyQualifiedName~PrReady|FullyQualifiedName~PrCallScanner'
classes='FullyQualifiedName~DynaDocs.Tests.Integration.GuardPrReadyTests.|FullyQualifiedName~DynaDocs.Tests.Commands.GuardPrReadyBodyFileTests.'
gate discovery 4G 240 dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --list-tests --filter "$light" || exit 1
gate node 4G 180 node --test DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs
gate classes 4G 600 dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-build --filter "$classes" \
  --logger 'console;verbosity=normal' --logger 'trx;LogFileName=classes.trx' --results-directory /tmp/dyd805-merge-classes
gate light 8G 1200 dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "$light" \
  --logger 'console;verbosity=normal' --logger 'trx;LogFileName=light.trx' --results-directory /tmp/dyd805-merge-light
gate static 8G 1800 .static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate static
