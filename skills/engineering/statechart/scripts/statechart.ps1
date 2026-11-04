# Native PowerShell entry; the POSIX launcher remains scripts/statechart.
$ErrorActionPreference = 'Stop'
$entry = Join-Path $PSScriptRoot 'statechart.ts'
if (Get-Command node -ErrorAction SilentlyContinue) {
    $version = [version]((node --version).TrimStart('v'))
    if (($version -ge [version]'22.18.0' -and $version -lt [version]'23.0.0') -or $version -ge [version]'23.6.0') {
        & node --disable-warning=ExperimentalWarning $entry @args
        exit $LASTEXITCODE
    }
}
Write-Error 'statechart on PowerShell needs Node 22.18 to 22.x, or Node 23.6 or later.' -ErrorAction Continue
exit 127
