param([Parameter(Mandatory)][string]$Tap)
$ErrorActionPreference = 'Stop'

# node exits 0 when every test in a file skips or the file has none, so a green run proves nothing
# until its TAP summary shows that each test ran and passed. A file run directly with no tests never
# starts the runner and writes no report.
if (-not (Test-Path -LiteralPath $Tap -PathType Leaf)) { throw "No test ran: there is no TAP report at $Tap." }
$counters = 'tests', 'pass', 'fail', 'cancelled', 'skipped', 'todo'
$counts = @{}
foreach ($line in Get-Content -LiteralPath $Tap) {
  if ($line -match '^# (tests|pass|fail|cancelled|skipped|todo) (\d+)$') { $counts[$Matches[1]] = [int]$Matches[2] }
}
$missing = @($counters | Where-Object { -not $counts.ContainsKey($_) })
if ($missing) { throw "The TAP report has no summary for: $($missing -join ', ')." }
$summary = ($counters | ForEach-Object { "$_=$($counts[$_])" }) -join ' '
if ($counts.tests -eq 0) { throw "No test ran: $summary" }
if ($counts.skipped -ne 0 -or $counts.todo -ne 0) { throw "A test skipped instead of running: $summary" }
if ($counts.pass -ne $counts.tests) { throw "Not every test passed: $summary" }
"Every test ran and passed: $summary"
