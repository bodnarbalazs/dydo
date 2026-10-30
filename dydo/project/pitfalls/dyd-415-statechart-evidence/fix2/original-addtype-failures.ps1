param([string]$SourcePath, [string]$OutputPath)
$ErrorActionPreference = 'Stop'

function Invoke-OriginalFailure([int]$TargetPid, [int]$ExpectedError) {
    try {
        & $SourcePath -WorkerId $TargetPid
        throw 'Original script unexpectedly accepted the failure fixture'
    } catch {
        $errorObject = $_.Exception
        while ($errorObject -and $errorObject -isnot [ComponentModel.Win32Exception]) {
            $errorObject = $errorObject.InnerException
        }
        if (-not $errorObject) { throw }
        @{
            pid = $TargetPid; expected = $ExpectedError; actual = $errorObject.NativeErrorCode
            passed = ($ExpectedError -eq $errorObject.NativeErrorCode)
            type = $errorObject.GetType().FullName; message = $errorObject.Message
        }
    }
}

$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = 'powershell.exe'
$start.Arguments = '-NoProfile -NonInteractive -Command "[Console]::In.ReadToEnd() | Out-Null"'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$child = [Diagnostics.Process]::Start($start)
try {
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $sourceHash = [BitConverter]::ToString($hasher.ComputeHash([IO.File]::ReadAllBytes($SourcePath))).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    $null = $child.Handle
    $child.Kill()
    if (-not $child.WaitForExit(5000)) { throw 'Owned process did not exit' }
    $cases = @(
        Invoke-OriginalFailure 0 87
        Invoke-OriginalFailure $child.Id 5
    )
    @{
        source = $SourcePath; sourceSha256 = $sourceHash
        powershell = $PSVersionTable.PSVersion.ToString(); clr = $PSVersionTable.CLRVersion.ToString()
        cases = $cases; ownedChildExited = $child.HasExited; hiddenChild = $start.CreateNoWindow
    } | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $OutputPath
    if (@($cases | Where-Object { -not $_.passed }).Count -gt 0) { exit 1 }
} finally {
    if (-not $child.HasExited) { $child.Kill(); $null = $child.WaitForExit(5000) }
    $child.Dispose()
}
