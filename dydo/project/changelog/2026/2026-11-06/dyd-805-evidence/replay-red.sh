#!/usr/bin/env bash
# Run from the repository root under the cap/lock in the adjacent changelog. Expected exit: 1.
set -euo pipefail
source_path=DynaDocs.Tests/Integration/GuardPrReadyTests.cs
evidence=dydo/project/changelog/2026/2026-11-06/dyd-805-evidence
scratch=$(mktemp -d /tmp/dyd805-red.XXXXXX)
cp "$source_path" "$scratch/candidate.cs"
trap 'cp "$scratch/candidate.cs" "$source_path"; cmp "$scratch/candidate.cs" "$source_path"; rm -rf "$scratch"' EXIT
git show 80b97b4dd6dcaa31bade1911e57cd3644d8d7a16:"$source_path" > "$scratch/base.cs"
awk -v snippet="$evidence/red-method.cs.txt" '
    /    \/\/ R3-B1: bash opens a relative body file/ {
        while ((getline line < snippet) > 0) print line
        close(snippet)
    }
    { print }
' "$scratch/base.cs" > "$source_path"
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore \
    --filter FullyQualifiedName~Dyd805_WindowsExpansionProbe \
    --logger 'console;verbosity=normal' --logger 'trx;LogFileName=red.trx' \
    --results-directory /tmp/dyd805-red-results
