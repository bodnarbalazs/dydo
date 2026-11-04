$ErrorActionPreference = 'Stop'

# setup-dotnet owns this isolated SDK directory. Add only the patched shared runtime;
# keep its SDK and non-versioned host files intact.
if (-not $env:DOTNET_ROOT) { throw 'Setup .NET must establish DOTNET_ROOT first.' }
if ((dotnet --version) -ne '10.0.303') { throw 'Expected .NET SDK 10.0.303 before runtime provisioning.' }

if ($IsWindows) {
    $installer = Join-Path $env:RUNNER_TEMP 'dotnet-install.ps1'
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installer
    & $installer -Runtime dotnet -Version 10.0.14 -InstallDir $env:DOTNET_ROOT -NoPath -SkipNonVersionedFiles
} else {
    $installer = Join-Path $env:RUNNER_TEMP 'dotnet-install.sh'
    Invoke-WebRequest https://dot.net/v1/dotnet-install.sh -OutFile $installer
    bash $installer --runtime dotnet --version 10.0.14 --install-dir $env:DOTNET_ROOT --no-path --skip-non-versioned-files
    if ($LASTEXITCODE -ne 0) { throw 'Patched .NET runtime installation failed.' }
}

if ((dotnet --version) -ne '10.0.303') { throw 'Runtime provisioning changed the selected SDK.' }
$runtimes = dotnet --list-runtimes
if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft.NETCore.App 10\.0\.14 \[')) {
    throw 'Expected Microsoft.NETCore.App 10.0.14 in the selected .NET installation.'
}
$runtimes
