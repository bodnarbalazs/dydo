$scripts = Join-Path (Get-Location) 'skills/engineering/statechart/scripts'
Describe 'PowerShell statechart entry' {
    It 'forwards a supported runtime and its exit status' {
        function global:node { if ($args[0] -eq '--version') { 'v22.23.2' } else { $global:LASTEXITCODE = 1 } }
        try {
            & (Join-Path $scripts 'statechart.ps1') 'test.tla' '--out' 'out'
            $LASTEXITCODE | Should Be 1
        } finally { Remove-Item Function:\node }
    }
    It 'rejects an unsupported runtime' {
        function global:node { 'v22.17.0' }
        try {
            & (Join-Path $scripts 'statechart.ps1') 2>$null
            $LASTEXITCODE | Should Be 127
        } finally { Remove-Item Function:\node }
    }
    It 'rejects a missing runtime' {
        $oldPath = $env:PATH
        try {
            $env:PATH = ''
            & (Join-Path $scripts 'statechart.ps1') 2>$null
            $LASTEXITCODE | Should Be 127
        } finally { $env:PATH = $oldPath }
    }
}
Describe 'PowerShell native job adapter' {
    It 'owns and terminates its task-created worker' {
        $worker = Start-Process node -ArgumentList '-e', 'setInterval(()=>{},1000)' -PassThru -WindowStyle Hidden
        $oldInput = [Console]::In
        try {
            [Console]::SetIn([System.IO.StringReader]::new(''))
            & (Join-Path $scripts 'windows-job.ps1') -WorkerId $worker.Id
            $worker.WaitForExit(5000) | Should Be $true
        } finally {
            [Console]::SetIn($oldInput)
            if (-not $worker.HasExited) { $worker.Kill() }
            $worker.Dispose()
        }
    }
}
