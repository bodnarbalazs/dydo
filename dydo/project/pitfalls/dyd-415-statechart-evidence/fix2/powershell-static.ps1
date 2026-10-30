param([string]$Root, [string]$ModulePath, [string]$OutputPath)
$ErrorActionPreference = 'Stop'
Import-Module $ModulePath -Force
if ((Get-Module PSScriptAnalyzer).Version.ToString() -ne '1.24.0') { throw 'Analyzer version mismatch' }
$rules = @(Get-ScriptAnalyzerRule | Select-Object RuleName, Severity, Description)
$files = @('statechart.ps1', 'windows-job.ps1') | ForEach-Object {
    $path = Join-Path $Root "skills/engineering/statechart/scripts/$_"
    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
    $diagnostics = @(Invoke-ScriptAnalyzer -Path $path | Select-Object RuleName, Severity, Message, Line, Column)
    $commands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true) | ForEach-Object {
        @{ name = $_.GetCommandName(); invocation = $_.InvocationOperator.ToString(); line = $_.Extent.StartLineNumber; text = $_.Extent.Text }
    })
    $functions = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true))
    $variables = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.VariableExpressionAst] }, $true) | ForEach-Object {
        @{ name = $_.VariablePath.UserPath; line = $_.Extent.StartLineNumber; parentKind = $_.Parent.GetType().Name }
    })
    $assignments = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.AssignmentStatementAst] }, $true) | ForEach-Object {
        @{ target = $_.Left.Extent.Text; line = $_.Extent.StartLineNumber }
    })
    @{
        path = $path; parseErrors = @($parseErrors | ForEach-Object { $_.Message })
        parameters = $(if ($ast.ParamBlock) { $ast.ParamBlock.Parameters.Count } else { 0 }); functionCount = $functions.Count
        diagnostics = $diagnostics; commands = $commands; variables = $variables; assignments = $assignments
    }
}
$harness = @($PSCommandPath, (Join-Path $PSScriptRoot 'original-addtype-failures.ps1')) | ForEach-Object {
    @{ path = $_; diagnostics = @(Invoke-ScriptAnalyzer -Path $_ | Select-Object RuleName, Severity, Message, Line, Column) }
}
@{ powershell = $PSVersionTable.PSVersion.ToString(); clr = $PSVersionTable.CLRVersion.ToString(); process64 = [Environment]::Is64BitProcess; analyzer = (Get-Module PSScriptAnalyzer).Version.ToString(); rules = $rules; files = @($files); harness = @($harness) } |
    ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 $OutputPath
if (@($files | Where-Object { $_.parseErrors.Count -gt 0 -or $_.diagnostics.Count -gt 0 }).Count -gt 0) { exit 1 }
if (@($harness | Where-Object { $_.diagnostics.Count -gt 0 }).Count -gt 0) { exit 1 }
