$ErrorActionPreference = 'Stop'
Import-Module Pester -RequiredVersion 3.4.0
$evidence = Join-Path (Get-Location) 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1'
$sources = @('statechart.ps1', 'windows-job.ps1') | ForEach-Object { Join-Path (Get-Location) "skills/engineering/statechart/scripts/$_" }
$capabilities = @{
    powershell = $PSVersionTable.PSVersion.ToString()
    pester = (Get-Module Pester).Version.ToString()
    scriptAnalyzer = @(Get-Module -ListAvailable PSScriptAnalyzer | ForEach-Object { $_.Version.ToString() })
    sources = @($sources | ForEach-Object {
        $tokens = $null
        $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($_, [ref]$tokens, [ref]$errors)
        @{ file = $_; parseErrors = @($errors | ForEach-Object { $_.Message }); parameters = $ast.ParamBlock.Parameters.Count }
    })
}
$capabilities | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'powershell-capabilities.json')
$result = Invoke-Pester -Script (Join-Path $evidence 'powershell.Tests.ps1') -CodeCoverage $sources -PassThru -Quiet
$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'powershell-coverage.json')
if ($result.FailedCount -gt 0) { exit 1 }
