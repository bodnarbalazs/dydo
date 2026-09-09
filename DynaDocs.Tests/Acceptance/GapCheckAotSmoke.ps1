[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('dydo gap-check aot ' + [Guid]::NewGuid().ToString('N'))

try {
    $project = Join-Path $scratch 'project root'
    $nested = Join-Path $project 'nested start'
    New-Item -ItemType Directory -Path $nested -Force | Out-Null
    $probe = Join-Path $project 'probe.py'
    @'
import json
import os
import sys

print(json.dumps({"arguments": sys.argv[1:], "currentDirectory": os.getcwd()}), end="")
print("probe stderr", end="", file=sys.stderr)
raise SystemExit(23)
'@ | Set-Content -LiteralPath $probe -NoNewline

    @'
{
  "testing": {
    "runner": ["py", "probe.py", "inspect", "", "fixed space", "fixed\"quote", "固定λ"]
  }
}
'@ | Set-Content -LiteralPath (Join-Path $project 'dydo.json') -NoNewline

    $caller = @('', 'caller space', 'caller"quote', '雪/é', '--unknown', 'one', '--unknown', 'two', '--', '--after')
    $stdout = Join-Path $scratch 'stdout.txt'
    $stderr = Join-Path $scratch 'stderr.txt'
    Push-Location $nested
    try {
        & $Executable gap-check @caller 1>$stdout 2>$stderr
        if ($LASTEXITCODE -ne 23) { throw "Expected child exit 23, got $LASTEXITCODE." }
    }
    finally {
        Pop-Location
    }

    $result = Get-Content -LiteralPath $stdout -Raw | ConvertFrom-Json
    $expected = @('inspect', '', 'fixed space', 'fixed"quote', '固定λ') + $caller
    if (@($result.arguments).Count -ne $expected.Count) { throw 'Runner argument count differs.' }
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if ($result.arguments[$index] -cne $expected[$index]) { throw "Runner argument $index differs." }
    }
    if ($result.currentDirectory -cne $project) { throw 'Runner did not use the configuration directory.' }
    if ((Get-Content -LiteralPath $stderr -Raw).TrimEnd("`r", "`n") -cne 'probe stderr') { throw 'Runner stderr was not inherited distinctly.' }
}
finally {
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
