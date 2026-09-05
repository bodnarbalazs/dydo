[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CandidateSha,

    [Parameter(Mandatory)]
    [string]$RollbackPackage
)

$ErrorActionPreference = 'Stop'
$betaVersion = '3.0.0-beta.1'
$rollbackVersion = '2.2.9'
$rollbackHash = 'C60F0D7395B1842DFF22E41914430D884FB7B3CCFF1A1059AE9FE7385695DB14'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$originalLocation = (Get-Location).Path
$runRoot = Join-Path $root ('dydo\_system\.local\dyd110-beta\' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
$packageRoot = Join-Path $runRoot 'packages'
$toolRoot = Join-Path $runRoot 'tool-path'
$scratch = Join-Path $runRoot 'scratch project'
$evidence = [ordered]@{
    candidate_sha = $CandidateSha
    beta_version = $betaVersion
    rollback_version = $rollbackVersion
    global_mutation_started = $false
    isolated_install = $false
    rollback_attempted = $false
    final_beta_reinstall = $false
    generated_role_canary = 'PENDING — fresh task/session required'
    static_gates = 'UNAVAILABLE — DYD-96 remains open'
    mutation = 'UNAVAILABLE — DYD-103 remains open'
}

function Invoke-Checked([scriptblock]$Action, [string]$Name) {
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

function Assert-Version([string]$Command, [string]$Expected, [string]$Name) {
    $actual = (& $Command version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $actual -ne "dydo version $Expected") {
        throw "$Name did not report dydo version $Expected."
    }
}

function Get-ManagedSnapshot([string]$Project) {
    $roots = @('.claude\agents', '.claude\skills', '.agents\skills', '.codex\agents') |
        ForEach-Object { Join-Path $Project $_ } |
        Where-Object { Test-Path $_ }
    return @($roots | ForEach-Object {
        Get-ChildItem -File -Recurse $_ | ForEach-Object {
            [ordered]@{ path = [IO.Path]::GetRelativePath($Project, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
        }
    } | Sort-Object path)
}

function Restore-Rollback {
    $evidence.rollback_attempted = $true
    Invoke-Checked { dotnet tool update --global dydo --add-source $rollbackSource --version $rollbackVersion --allow-downgrade --ignore-failed-sources } 'rollback install'
    Assert-Version $globalCommand.Source $rollbackVersion 'rollback command'
}

New-Item -ItemType Directory -Force -Path $packageRoot, $toolRoot, $scratch | Out-Null
$rollbackPackage = (Resolve-Path $RollbackPackage).Path
$rollbackSource = Split-Path -Parent $rollbackPackage
$beforePath = @{
    process = [Environment]::GetEnvironmentVariable('PATH', 'Process')
    user = [Environment]::GetEnvironmentVariable('PATH', 'User')
    machine = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
}

try {
    Set-Location $root
    $actualSha = (git rev-parse HEAD).Trim()
    if ($actualSha -ne $CandidateSha) { throw 'Candidate SHA does not match HEAD.' }
    if (git status --porcelain) { throw 'Candidate is dirty; refusing package or global mutation.' }
    if ((Get-FileHash $rollbackPackage -Algorithm SHA256).Hash -ne $rollbackHash) { throw 'Rollback package SHA-256 does not match the retained 2.2.9 package.' }

    $globalCommand = Get-Command dydo -CommandType Application -ErrorAction Stop
    Assert-Version $globalCommand.Source $rollbackVersion 'preexisting global command'
    $evidence.preexisting_command_path_sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($globalCommand.Source)))

    Invoke-Checked { dotnet pack DynaDocs.csproj -c Release --no-restore -o $packageRoot } 'beta package'
    $package = Join-Path $packageRoot "dydo.$betaVersion.nupkg"
    if (!(Test-Path $package)) { throw 'Expected beta NuGet package was not produced.' }
    $evidence.package_sha256 = (Get-FileHash $package -Algorithm SHA256).Hash

    Invoke-Checked { dotnet tool install --tool-path $toolRoot dydo --add-source $packageRoot --version $betaVersion --ignore-failed-sources } 'isolated beta install'
    $isolated = Join-Path $toolRoot 'dydo.exe'
    if (!(Test-Path $isolated)) { throw 'The isolated beta command was not created.' }
    Assert-Version $isolated $betaVersion 'isolated beta command'
    Invoke-Checked { & $isolated --help } 'isolated help'
    Set-Location $scratch
    Invoke-Checked { & $isolated init all } 'isolated init'
    Invoke-Checked { & $isolated check } 'isolated check'
    Invoke-Checked { & $isolated sync } 'isolated first sync'
    $firstSnapshot = Get-ManagedSnapshot $scratch | ConvertTo-Json -Compress
    Invoke-Checked { & $isolated 'template' 'update' } 'isolated template update'
    Invoke-Checked { & $isolated sync } 'isolated second sync'
    $secondSnapshot = Get-ManagedSnapshot $scratch | ConvertTo-Json -Compress
    if ($firstSnapshot -ne $secondSnapshot) { throw 'The isolated second sync changed compiler-owned artifacts.' }
    Set-Location $root
    $evidence.isolated_install = $true
    $evidence.isolated_command_path_sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($isolated)))
    $evidence.isolated_sync_idempotent = $true

    $evidence.global_mutation_started = $true
    Invoke-Checked { dotnet tool update --global dydo --add-source $packageRoot --version $betaVersion --ignore-failed-sources } 'global beta install'
    $globalCommand = Get-Command dydo -CommandType Application -ErrorAction Stop
    Assert-Version $globalCommand.Source $betaVersion 'global beta command'
    Restore-Rollback
    Invoke-Checked { dotnet tool update --global dydo --add-source $packageRoot --version $betaVersion --ignore-failed-sources } 'final global beta install'
    $globalCommand = Get-Command dydo -CommandType Application -ErrorAction Stop
    Assert-Version $globalCommand.Source $betaVersion 'final global beta command'
    $evidence.final_beta_reinstall = $true
}
catch {
    $evidence.error = $_.Exception.Message
    if ($evidence.global_mutation_started -and -not $evidence.rollback_attempted) {
        try { Restore-Rollback } catch { $evidence.rollback_error = $_.Exception.Message }
    }
    throw
}
finally {
    Set-Location $originalLocation
    $afterPath = @{
        process = [Environment]::GetEnvironmentVariable('PATH', 'Process')
        user = [Environment]::GetEnvironmentVariable('PATH', 'User')
        machine = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
    }
    $evidence.path_bytes_unchanged = $beforePath.process -ceq $afterPath.process -and $beforePath.user -ceq $afterPath.user -and $beforePath.machine -ceq $afterPath.machine
    $evidence | ConvertTo-Json -Depth 4 | Set-Content -NoNewline (Join-Path $runRoot 'evidence.json')
    if (-not $evidence.path_bytes_unchanged) { throw 'PATH bytes changed during beta acceptance.' }
}
