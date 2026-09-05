[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CandidateSha,

    [Parameter(Mandatory)]
    [string]$RollbackPackage,

    [switch]$IsolatedOnly
)

$ErrorActionPreference = 'Stop'
$betaVersion = '3.0.0-beta.1'
$rollbackVersion = '2.2.9'
$rollbackHash = 'C60F0D7395B1842DFF22E41914430D884FB7B3CCFF1A1059AE9FE7385695DB14'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$originalLocation = (Get-Location).Path
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$runRoot = Join-Path $root ('dydo\_system\.local\dyd110-beta\' + $runId)
$packageRoot = Join-Path $runRoot 'packages'
$toolRoot = Join-Path $runRoot 'tool-path'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$scratchRoot = Join-Path $temporaryRoot ('dyd110-beta-' + $runId)
$scratch = Join-Path $scratchRoot 'scratch project'
$evidence = [ordered]@{
    candidate_sha = $CandidateSha
    beta_version = $betaVersion
    rollback_version = $rollbackVersion
    global_mutation_started = $false
    isolated_install = $false
    scratch_removed = $false
    rollback_attempted = $false
    final_beta_reinstall = $false
    isolated_only = [bool]$IsolatedOnly
    generated_role_canary = 'PENDING - fresh task/session required'
    static_gates = 'UNAVAILABLE - DYD-96 remains open'
    mutation = 'UNAVAILABLE - DYD-103 remains open'
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

function Assert-InstalledPackage([string]$ToolDirectory, [string]$Version, [string]$ExpectedHash, [string]$Name) {
    $installedPackage = Join-Path $ToolDirectory ".store\dydo\$Version\dydo\$Version\dydo.nupkg"
    if (-not (Test-Path -LiteralPath $installedPackage)) {
        throw "$Name package is missing from the installed tool store."
    }
    $actualHash = (Get-FileHash $installedPackage -Algorithm SHA256).Hash
    if ($actualHash -ne $ExpectedHash) {
        throw "$Name package bytes do not match the reviewed local package."
    }
    return $actualHash
}

function Get-StringHash([string]$Value) {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
    }
}

function Write-Json([object]$Value, [string]$Path) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
}

function Get-RelativePath([string]$BasePath, [string]$Path) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Managed artifact is outside the scratch project: $fullPath"
    }
    return $fullPath.Substring($base.Length).Replace('\', '/')
}

function Get-ManagedSnapshot([string]$Project) {
    $roots = @('.claude\agents', '.claude\skills', '.agents\skills', '.codex\agents') |
        ForEach-Object { Join-Path $Project $_ } |
        Where-Object { Test-Path $_ }
    return @($roots | ForEach-Object {
        Get-ChildItem -File -Recurse $_ | ForEach-Object {
            [ordered]@{ path = Get-RelativePath $Project $_.FullName; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
        }
    } | Sort-Object path)
}

function Get-TemplateSnapshot {
    $templates = Join-Path $root 'Templates'
    return @(Get-ChildItem -File -Recurse $templates | ForEach-Object {
        [ordered]@{ path = Get-RelativePath $root $_.FullName; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    } | Sort-Object path)
}

function Restore-Rollback {
    $evidence.rollback_attempted = $true
    Invoke-Checked { dotnet tool update --global dydo --source $rollbackSource --version $rollbackVersion --allow-downgrade } 'rollback install'
    $rollbackCommand = Get-Command dydo -CommandType Application -ErrorAction Stop
    if ($rollbackCommand.Source -cne $globalCommandPath) { throw 'Rollback changed the PATH-resolved dydo command.' }
    Assert-Version $rollbackCommand.Source $rollbackVersion 'rollback command'
    $evidence.rollback_installed_package_sha256 = Assert-InstalledPackage (Split-Path -Parent $rollbackCommand.Source) $rollbackVersion $rollbackHash 'rollback'
    $evidence.rollback_command_path_sha256 = Get-StringHash $rollbackCommand.Source
}

New-Item -ItemType Directory -Force -Path $packageRoot, $toolRoot, $scratch | Out-Null
$resolvedRollbackPackage = $null
$rollbackSource = $null
$globalCommandPath = $null
$failure = $null
$beforePath = @{
    process = [Environment]::GetEnvironmentVariable('PATH', 'Process')
    user = [Environment]::GetEnvironmentVariable('PATH', 'User')
    machine = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
}

try {
    $resolvedRollbackPackage = (Resolve-Path $RollbackPackage).Path
    $rollbackSource = Split-Path -Parent $resolvedRollbackPackage
    Set-Location $root
    $actualSha = (& git rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the candidate Git SHA.' }
    if ($actualSha -ne $CandidateSha) { throw 'Candidate SHA does not match HEAD.' }
    $gitStatus = & git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Could not read candidate Git status.' }
    if ($gitStatus) { throw 'Candidate is dirty; refusing package or global mutation.' }
    if ((Get-FileHash $resolvedRollbackPackage -Algorithm SHA256).Hash -ne $rollbackHash) { throw 'Rollback package SHA-256 does not match the retained 2.2.9 package.' }

    $sourceManifest = Join-Path $runRoot 'source-templates.json'
    Write-Json (Get-TemplateSnapshot) $sourceManifest
    $evidence.source_template_manifest_sha256 = (Get-FileHash $sourceManifest -Algorithm SHA256).Hash

    $globalCommand = Get-Command dydo -CommandType Application -ErrorAction Stop
    $globalCommandPath = $globalCommand.Source
    Assert-Version $globalCommand.Source $rollbackVersion 'preexisting global command'
    $evidence.preexisting_command_path_sha256 = Get-StringHash $globalCommand.Source

    Invoke-Checked { dotnet pack DynaDocs.csproj -c Release --no-restore -o $packageRoot } 'beta package'
    $package = Join-Path $packageRoot "dydo.$betaVersion.nupkg"
    if (!(Test-Path $package)) { throw 'Expected beta NuGet package was not produced.' }
    $evidence.package_sha256 = (Get-FileHash $package -Algorithm SHA256).Hash

    Invoke-Checked { dotnet tool install --tool-path $toolRoot dydo --source $packageRoot --version $betaVersion } 'isolated beta install'
    $isolated = Join-Path $toolRoot 'dydo.exe'
    if (!(Test-Path $isolated)) { throw 'The isolated beta command was not created.' }
    Assert-Version $isolated $betaVersion 'isolated beta command'
    $evidence.isolated_installed_package_sha256 = Assert-InstalledPackage $toolRoot $betaVersion $evidence.package_sha256 'isolated beta'
    Invoke-Checked { & $isolated --help } 'isolated help'
    Set-Location $scratch
    Invoke-Checked { & $isolated init all } 'isolated init'
    Invoke-Checked { & $isolated check } 'isolated check'
    Invoke-Checked { & $isolated sync } 'isolated first sync'
    $firstSnapshot = @(Get-ManagedSnapshot $scratch)
    $firstSnapshotJson = $firstSnapshot | ConvertTo-Json -Compress
    Invoke-Checked { & $isolated 'template' 'update' } 'isolated template update'
    Invoke-Checked { & $isolated sync } 'isolated second sync'
    $secondSnapshot = @(Get-ManagedSnapshot $scratch)
    $secondSnapshotJson = $secondSnapshot | ConvertTo-Json -Compress
    if ($firstSnapshotJson -ne $secondSnapshotJson) { throw 'The isolated second sync changed compiler-owned artifacts.' }
    $emittedManifest = Join-Path $runRoot 'emitted-artifacts.json'
    Write-Json $secondSnapshot $emittedManifest
    $evidence.emitted_artifact_manifest_sha256 = (Get-FileHash $emittedManifest -Algorithm SHA256).Hash
    Set-Location $root
    $evidence.isolated_install = $true
    $evidence.isolated_command_path_sha256 = Get-StringHash $isolated
    $evidence.isolated_sync_idempotent = $true

    if (-not $IsolatedOnly) {
        $evidence.global_mutation_started = $true
        Invoke-Checked { dotnet tool update --global dydo --source $packageRoot --version $betaVersion } 'global beta install'
        $globalCommand = Get-Command dydo -CommandType Application -ErrorAction Stop
        if ($globalCommand.Source -cne $globalCommandPath) { throw 'Beta update changed the PATH-resolved dydo command.' }
        Assert-Version $globalCommand.Source $betaVersion 'global beta command'
        $evidence.beta_installed_package_sha256 = Assert-InstalledPackage (Split-Path -Parent $globalCommand.Source) $betaVersion $evidence.package_sha256 'global beta'
        $evidence.beta_command_path_sha256 = Get-StringHash $globalCommand.Source
        Restore-Rollback
        Invoke-Checked { dotnet tool update --global dydo --source $packageRoot --version $betaVersion } 'final global beta install'
        $globalCommand = Get-Command dydo -CommandType Application -ErrorAction Stop
        if ($globalCommand.Source -cne $globalCommandPath) { throw 'Final beta install changed the PATH-resolved dydo command.' }
        Assert-Version $globalCommand.Source $betaVersion 'final global beta command'
        $evidence.final_installed_package_sha256 = Assert-InstalledPackage (Split-Path -Parent $globalCommand.Source) $betaVersion $evidence.package_sha256 'final global beta'
        $evidence.final_command_path_sha256 = Get-StringHash $globalCommand.Source
        $evidence.final_beta_reinstall = $true
    }
}
catch {
    $failure = $_
    $evidence.error = $_.Exception.Message
    if ($evidence.global_mutation_started) {
        try { Restore-Rollback } catch { $evidence.rollback_error = $_.Exception.Message }
    }
}
finally {
    Set-Location $originalLocation
    if (Test-Path -LiteralPath $scratchRoot) {
        try {
            $temporaryPrefix = $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            $scratchRootPath = [IO.Path]::GetFullPath($scratchRoot)
            if (-not $scratchRootPath.StartsWith($temporaryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove scratch path outside the temporary root: $scratchRootPath"
            }
            Remove-Item -LiteralPath $scratchRootPath -Recurse -Force
            $evidence.scratch_removed = $true
        }
        catch {
            $evidence.cleanup_error = $_.Exception.Message
            if ($null -eq $failure) { $failure = $_ }
        }
    }
    $afterPath = @{
        process = [Environment]::GetEnvironmentVariable('PATH', 'Process')
        user = [Environment]::GetEnvironmentVariable('PATH', 'User')
        machine = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
    }
    $evidence.path_bytes_unchanged = $beforePath.process -ceq $afterPath.process -and $beforePath.user -ceq $afterPath.user -and $beforePath.machine -ceq $afterPath.machine
    if (-not $evidence.path_bytes_unchanged -and $null -eq $failure) {
        $failure = [InvalidOperationException]::new('PATH bytes changed during beta acceptance.')
        $evidence.error = $failure.Message
    }
    Write-Json $evidence (Join-Path $runRoot 'evidence.json')
}

if ($null -ne $failure) { throw $failure }
