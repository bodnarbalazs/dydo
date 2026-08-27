[CmdletBinding(DefaultParameterSetName = 'Verify')]
param(
    [Parameter(Mandatory = $true)] [string]$RepoRoot,
    [Parameter(ParameterSetName = 'Write', Mandatory = $true)] [switch]$Write,
    [Parameter(ParameterSetName = 'Verify', Mandatory = $true)] [switch]$Verify,
    [switch]$RequireRatified
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$projectRoot = Join-Path $repoRoot 'dydo/project'
$jsonPath = Join-Path $projectRoot 'migrations/3.0-pm-records.json'
$markdownPath = Join-Path $projectRoot 'migrations/3.0-pm-records.md'
$legacyFolders = [ordered]@{ campaigns = 'campaign'; sprints = 'sprint'; slices = 'slice'; tasks = 'task'; issues = 'issue'; backlog = 'backlog'; releases = 'release'; 'future-features' = 'future-feature' }
$rootExceptions = [ordered]@{ 'dydo/project/docs-upgrade-sprint.md' = 'sprint'; 'dydo/project/v1.3-release.md' = 'release'; 'dydo/project/v1.4-release.md' = 'release' }
$dispositions = @('migrate-initiative', 'migrate-project', 'migrate-issue', 'retain', 'retain-normalize', 'extract-then-remove', 'remove-historical', 'cancel-remove', 'drop-duplicate')
$targetKinds = @('linear-preview-key', 'linear-url', 'retained-path', 'commit-permalink', 'none')
$evidenceKinds = @('linear-readback', 'retained-path', 'freeze-commit', 'human-ruling')
$recordFields = @('path', 'kind', 'status', 'outsideCanonicalFolder', 'incomingReferences', 'proposedDisposition', 'finalDisposition', 'humanRatified', 'executionState', 'target', 'evidence', 'reason')
$referenceFields = @('sourcePath', 'line', 'rawTarget', 'resolution')
$targetFields = @('kind', 'value')
$evidenceFields = @('kind', 'value')
$excludedFields = @('path', 'matchedSignature', 'exclusionReason', 'humanRatified')
$unresolvedFields = @('path', 'matchedSignature')
$topFields = @('schemaVersion', 'generatedFromCommit', 'records', 'excludedCandidates', 'unresolvedCandidates', 'provenance')
$provenanceFields = @('source', 'counts', 'write', 'verify', 'dydoCheck')
$countFields = @('records', 'incomingReferences', 'excludedCandidates', 'unresolvedCandidates')
$gateFields = @('command', 'output', 'exitCode')
$currentStatuses = @('active', 'open', 'in-progress', 'pending', 'ready', 'in-flight', 'review-pending')
$closedStatuses = @('done', 'resolved', 'closed', 'cancelled', 'canceled', 'complete', 'completed')

function ConvertTo-RepoPath([string]$fullPath) { [IO.Path]::GetRelativePath($repoRoot, $fullPath).Replace('\', '/') }
function Test-PropertySet([object]$value, [string[]]$expected) {
    if ($null -eq $value) { return $false }
    $null -eq (Compare-Object @($value.PSObject.Properties.Name | Sort-Object) @($expected | Sort-Object))
}
function Get-FrontmatterValue([string[]]$lines, [string]$name) {
    if ($lines.Count -lt 3 -or $lines[0] -ne '---') { return $null }
    for ($index = 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -eq '---') { break }
        if ($lines[$index] -match "^$([regex]::Escape($name)):\s*(?<value>.*)$") {
            $value = $Matches.value.Trim()
            if ([string]::IsNullOrWhiteSpace($value)) { return $null }
            return $value.Trim('"')
        }
    }
    $null
}
function Get-HeadCommit {
    $head = & git -C $repoRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve HEAD.' }
    $head.Trim()
}
function Set-SourceCommit([string]$commit) {
    $resolved = @(& git -C $repoRoot rev-parse "${commit}^{commit}")
    if ($LASTEXITCODE -ne 0 -or $resolved.Count -ne 1) { throw "Unable to resolve migration source commit: $commit" }
    $script:sourceCommit = $resolved[0].Trim()
    Remove-Variable -Scope Script -Name headLinesByPath -ErrorAction Ignore
}
function Test-SourceCommit([string]$commit) {
    $resolved = @(& git -C $repoRoot rev-parse "${commit}^{commit}")
    if ($LASTEXITCODE -ne 0 -or $resolved.Count -ne 1 -or $resolved[0].Trim() -ne $commit) { throw "Manifest source commit is unavailable or invalid: $commit" }
    & git -C $repoRoot merge-base --is-ancestor $commit HEAD
    if ($LASTEXITCODE -ne 0) { throw "Manifest source commit is not an ancestor of the current HEAD: $commit" }
}
function Initialize-HeadTree {
    if (Get-Variable -Scope Script -Name headLinesByPath -ErrorAction Ignore) { return }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archivePath = Join-Path ([IO.Path]::GetTempPath()) ("dydo-3.0-pm-$PID.zip")
    try {
        & git -C $repoRoot archive --format=zip "--output=$archivePath" $script:sourceCommit
        if ($LASTEXITCODE -ne 0) { throw "Unable to archive migration source commit $script:sourceCommit." }
        $script:headLinesByPath = @{}
        $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
        try {
            foreach ($entry in $archive.Entries) {
                if (-not $entry.FullName.EndsWith('.md') -and $entry.FullName -ne 'dydo.json') { continue }
                $reader = [IO.StreamReader]::new($entry.Open())
                try { $script:headLinesByPath[$entry.FullName] = @($reader.ReadToEnd() -split "`r?`n") } finally { $reader.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force } }
}
function Get-HeadSourcePaths { Initialize-HeadTree; @($script:headLinesByPath.Keys | Sort-Object) }
function Get-HeadLines([string]$path) {
    Initialize-HeadTree
    if (-not $script:headLinesByPath.ContainsKey($path)) { throw "Unable to read HEAD:$path" }
    $script:headLinesByPath[$path]
}
function Get-FreezeCommitUrl {
    $sha = & git -C $repoRoot rev-parse 'pm-v2-final^{}' 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    "https://github.com/bodnarbalazs/dydo/commit/$($sha.Trim())"
}
function Test-FreezeCommitUrl([string]$value) {
    $freeze = Get-FreezeCommitUrl
    -not [string]::IsNullOrWhiteSpace($freeze) -and $value -eq $freeze
}
function Get-PreviewKey([string]$path, [string]$kind) {
    $slug = ([IO.Path]::GetFileNameWithoutExtension($path).ToUpperInvariant() -replace '[^A-Z0-9]+', '-').Trim('-')
    "DYD-3-PREVIEW-$($kind.ToUpperInvariant())-$slug"
}
function Get-ProposedRow([string]$path, [string]$kind, [string]$status, [string]$freezeCommitUrl) {
    $normalized = if ($null -eq $status) { '' } else { $status.ToLowerInvariant() }
    if ($kind -eq 'future-feature') { $disposition = 'retain-normalize' }
    elseif ($kind -in @('task', 'release') -or $normalized -in $closedStatuses) { $disposition = 'remove-historical' }
    elseif ($kind -in @('campaign', 'sprint') -and $normalized -in $currentStatuses) { $disposition = 'migrate-project' }
    elseif ($kind -in @('slice', 'issue', 'backlog') -and $normalized -in $currentStatuses) { $disposition = 'migrate-issue' }
    else { $disposition = 'remove-historical' }
    if ($disposition -in @('migrate-initiative', 'migrate-project', 'migrate-issue')) { $target = [ordered]@{ kind = 'linear-preview-key'; value = Get-PreviewKey $path $kind }; $evidence = @() }
    elseif ($disposition -in @('retain', 'retain-normalize')) { $target = [ordered]@{ kind = 'retained-path'; value = $path }; $evidence = @([ordered]@{ kind = 'retained-path'; value = $path }) }
    elseif (-not [string]::IsNullOrWhiteSpace($freezeCommitUrl)) { $target = [ordered]@{ kind = 'commit-permalink'; value = $freezeCommitUrl }; $evidence = @([ordered]@{ kind = 'freeze-commit'; value = $freezeCommitUrl }) }
    else { $target = [ordered]@{ kind = 'none'; value = '' }; $evidence = @() }
    $reason = switch ($disposition) {
        'retain-normalize' { 'FutureFeature is a repo-native idea record. It remains unpromoted until a separate human decision creates Linear work.' }
        'migrate-project' { 'Current campaign or sprint status makes this a candidate for human review as Linear Project work; this draft does not authorize migration.' }
        'migrate-issue' { 'Current work status makes this a candidate for human review as a Linear Issue; this draft does not authorize migration.' }
        default { 'Legacy execution history is proposed for removal from the default branch after the exact pm-v2-final freeze commit is available and human ratification is recorded.' }
    }
    [ordered]@{ path = $path; kind = $kind; status = $status; outsideCanonicalFolder = $false; incomingReferences = @(); proposedDisposition = $disposition; finalDisposition = $null; humanRatified = $false; executionState = 'pending'; target = $target; evidence = $evidence; reason = $reason }
}
function Get-CandidateSignature([string[]]$lines) {
    $type = Get-FrontmatterValue $lines 'type'
    $status = Get-FrontmatterValue $lines 'status'
    $title = Get-FrontmatterValue $lines 'title'
    $frontmatter = if ($lines.Count -gt 0 -and $lines[0] -eq '---') { $lines[1..([Array]::IndexOf($lines, '---', 1) - 1)] } else { @() }
    $legacyField = $frontmatter | Where-Object { $_ -match '^(seq|assigned|sprint|slice|task):\s*' } | Select-Object -First 1
    if ($type -in @('campaign', 'sprint', 'slice', 'task', 'issue', 'backlog', 'release', 'future-feature')) { return "type: $type; status: $status; title: $title" }
    if ($null -ne $legacyField) { return $legacyField.Trim() }
    $null
}
function Read-ExistingManifest {
    if (-not (Test-Path -LiteralPath $jsonPath)) { return $null }
    try { Get-Content -Raw -LiteralPath $jsonPath | ConvertFrom-Json -Depth 20 }
    catch { throw "Existing manifest cannot be preserved because it is invalid JSON: $($_.Exception.Message)" }
}
function Merge-CandidateReview([object[]]$candidates, [object]$existing) {
    $existingExclusions = if ($null -ne $existing -and $null -ne $existing.excludedCandidates) { @($existing.excludedCandidates) } else { @() }
    $excluded = [System.Collections.Generic.List[object]]::new(); $unresolved = [System.Collections.Generic.List[object]]::new()
    foreach ($candidate in $candidates) {
        $match = @($existingExclusions | Where-Object { $_.path -eq $candidate.path -and $_.matchedSignature -eq $candidate.matchedSignature } | Select-Object -First 1)
        if ($match.Count -eq 1 -and (Test-PropertySet $match[0] $excludedFields) -and $match[0].humanRatified -eq $true -and -not [string]::IsNullOrWhiteSpace($match[0].exclusionReason)) { $excluded.Add([ordered]@{ path = $match[0].path; matchedSignature = $match[0].matchedSignature; exclusionReason = $match[0].exclusionReason; humanRatified = $true }) }
        else { $unresolved.Add([ordered]@{ path = $candidate.path; matchedSignature = $candidate.matchedSignature }) }
    }
    [ordered]@{ excluded = @($excluded | Sort-Object path); unresolved = @($unresolved | Sort-Object path) }
}
function Get-ReferenceResolution([object]$record) {
    $disposition = if ($record.humanRatified) { $record.finalDisposition } else { $record.proposedDisposition }
    switch ($disposition) {
        { $_ -in @('migrate-initiative', 'migrate-project', 'migrate-issue') } { 'rewrite-linear'; break }
        { $_ -in @('retain', 'retain-normalize') } { 'unchanged'; break }
        { $_ -eq 'extract-then-remove' } { 'rewrite-retained'; break }
        default { 'rewrite-commit-permalink' }
    }
}
function Resolve-ReferenceTarget([string]$sourcePath, [string]$rawTarget) {
    $target = $rawTarget.Split('#?')[0]
    if ([string]::IsNullOrWhiteSpace($target) -or $target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { return $null }
    if ($target.StartsWith('dydo/project/')) { return $target.Replace('\', '/') }
    $sourceDirectory = Split-Path -Parent (Join-Path $repoRoot $sourcePath)
    ConvertTo-RepoPath ([IO.Path]::GetFullPath([IO.Path]::Combine($sourceDirectory, $target)))
}
function Test-AndConsumeReferenceSpan([System.Collections.Generic.HashSet[int]]$consumed, [int]$start, [int]$length) {
    for ($offset = $start; $offset -lt $start + $length; $offset++) { if ($consumed.Contains($offset)) { return $false } }
    for ($offset = $start; $offset -lt $start + $length; $offset++) { [void]$consumed.Add($offset) }
    $true
}
function Test-IsGenericIssueIdExample([string]$line, [int]$start, [int]$length) {
    $before = $line.Substring([Math]::Max(0, $start - 48), [Math]::Min(48, $start))
    $afterStart = $start + $length
    $after = $line.Substring($afterStart, [Math]::Min(32, $line.Length - $afterStart))
    if ($before -match '(?i)(?:\be\.g\.\,?\s*|\bexample\s*[:(]?\s*|\bformat\s*[:(]\s*|\bschema\s*[:(]\s*|\bissue\s+id\s*\(\s*)$') { return $true }
    if ($before -match '(?i)\bissues?\s+are\s+numbered\s*\(\s*(?:\d{4}\s*,\s*)*$') { return $true }
    $line -match '(?i)^\s*dydo\s+issue\s+\w+\s+\d{4}(?:\s|$)'
}
function Add-IncomingReferences([object[]]$records, [hashtable]$byPath, [string[]]$sourcePaths) {
    $issueIds = @{}
    foreach ($record in $records) { if ($record.kind -eq 'issue' -and ([IO.Path]::GetFileName($record.path) -match '^(?<id>\d{4})-')) { $issueIds[$Matches.id] = $record.path } }
    foreach ($sourcePath in $sourcePaths) {
        $lines = Get-HeadLines $sourcePath
        for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
            $line = $lines[$lineIndex]; $consumed = [System.Collections.Generic.HashSet[int]]::new()
            foreach ($match in [regex]::Matches($line, '\[(?<label>[^\]]*)\]\((?<target>[^)\s#]+)(?:#[^)\s]*)?\)|(?<target>dydo/project/[A-Za-z0-9_./-]+\.md)')) {
                if (-not (Test-AndConsumeReferenceSpan $consumed $match.Index $match.Length)) { continue }
                $rawTarget = $match.Groups['target'].Value; $candidate = Resolve-ReferenceTarget $sourcePath $rawTarget
                if ($null -ne $candidate -and $byPath.ContainsKey($candidate)) { $record = $byPath[$candidate]; $record.incomingReferences += [ordered]@{ sourcePath = $sourcePath; line = $lineIndex + 1; rawTarget = $rawTarget; resolution = Get-ReferenceResolution $record } }
            }
            foreach ($match in [regex]::Matches($line, '(?i)(?<![A-Za-z0-9])(?:issue\s*[:#]?\s*|#\s*|\[)(?<id>\d{4})(?:\]|(?!\d))')) {
                if (-not (Test-AndConsumeReferenceSpan $consumed $match.Index $match.Length)) { continue }
                $id = $match.Groups['id'].Value
                if ($issueIds.ContainsKey($id)) { $candidate = $issueIds[$id]; $record = $byPath[$candidate]; $record.incomingReferences += [ordered]@{ sourcePath = $sourcePath; line = $lineIndex + 1; rawTarget = $match.Value; resolution = Get-ReferenceResolution $record } }
            }
            foreach ($match in [regex]::Matches($line, '(?<![A-Za-z0-9#-])(?<id>\d{4})(?![A-Za-z0-9-]|\.md\b)')) {
                if (-not (Test-AndConsumeReferenceSpan $consumed $match.Index $match.Length)) { continue }
                $id = $match.Groups['id'].Value
                $prefix = $line.Substring(0, $match.Index)
                if ($prefix -match '(?i)(?:issue\s*[:#]?\s*|#\s*|\[)$') { continue }
                if (Test-IsGenericIssueIdExample $line $match.Index $match.Length) { continue }
                if ($issueIds.ContainsKey($id)) { $candidate = $issueIds[$id]; $record = $byPath[$candidate]; $record.incomingReferences += [ordered]@{ sourcePath = $sourcePath; line = $lineIndex + 1; rawTarget = $match.Value; resolution = Get-ReferenceResolution $record } }
            }
        }
    }
}
function Get-Inventory([object]$existingManifest) {
    $records = [System.Collections.Generic.List[object]]::new(); $recordPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $sourcePaths = Get-HeadSourcePaths; $recordCandidates = @($sourcePaths | Where-Object { $_.EndsWith('.md') -and -not [IO.Path]::GetFileName($_).StartsWith('_') }); $freezeCommitUrl = Get-FreezeCommitUrl
    foreach ($folder in $legacyFolders.Keys) {
        foreach ($path in @($recordCandidates | Where-Object { $_.StartsWith("dydo/project/$folder/", [StringComparison]::OrdinalIgnoreCase) })) { $row = Get-ProposedRow $path $legacyFolders[$folder] (Get-FrontmatterValue (Get-HeadLines $path) 'status') $freezeCommitUrl; $records.Add($row); [void]$recordPaths.Add($path) }
    }
    foreach ($exceptionPath in $rootExceptions.Keys) {
        if ($exceptionPath -notin $recordCandidates) { throw "Required root exception is missing from HEAD: $exceptionPath" }
        $row = Get-ProposedRow $exceptionPath $rootExceptions[$exceptionPath] (Get-FrontmatterValue (Get-HeadLines $exceptionPath) 'status') $freezeCommitUrl; $row.outsideCanonicalFolder = $true; $records.Add($row); [void]$recordPaths.Add($exceptionPath)
    }
    $candidates = [System.Collections.Generic.List[object]]::new()
    foreach ($path in $recordCandidates) { if (-not $recordPaths.Contains($path)) { $signature = Get-CandidateSignature (Get-HeadLines $path); if ($null -ne $signature) { $candidates.Add([ordered]@{ path = $path; matchedSignature = $signature }) } } }
    $review = Merge-CandidateReview @($candidates | Sort-Object path) $existingManifest
    $orderedRecords = @($records | Sort-Object path); $byPath = @{}; foreach ($record in $orderedRecords) { $byPath[$record.path] = $record }; Add-IncomingReferences $orderedRecords $byPath $sourcePaths
    [ordered]@{ schemaVersion = 1; generatedFromCommit = $script:sourceCommit; records = $orderedRecords; excludedCandidates = $review.excluded; unresolvedCandidates = $review.unresolved }
}
function Get-ReferenceKey([object]$reference) { "$($reference.sourcePath):$($reference.line):$($reference.rawTarget)" }
function Get-CheckTranscript {
    $output = @(& dydo check 2>&1); $exitCode = $LASTEXITCODE; $summary = @($output | Where-Object { $_ -match '^Found \d+ errors, \d+ warnings' } | Select-Object -Last 1)
    if ($exitCode -ne 0 -or $summary.Count -ne 1) { throw "dydo check failed while producing manifest evidence (exit $exitCode)." }
    [pscustomobject]@{ exitCode = $exitCode; summary = $summary[0].Trim() }
}
function Get-Provenance([object]$manifest, [bool]$verificationComplete, [object]$checkTranscript) {
    $references = @($manifest.records | ForEach-Object { @($_.incomingReferences) }).Count
    $counts = [ordered]@{ records = $manifest.records.Count; incomingReferences = $references; excludedCandidates = $manifest.excludedCandidates.Count; unresolvedCandidates = $manifest.unresolvedCandidates.Count }
    $check = if ($null -eq $checkTranscript) { Get-CheckTranscript } else { $checkTranscript }
    $checkOutput = if ($null -ne $check.PSObject.Properties['summary']) { $check.summary } else { $check.output }
    if ($verificationComplete) { $verify = [ordered]@{ command = 'pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Verify'; output = "manifest verified: $($counts.records) records; 0 missing; 0 duplicates; 0 invalid references; $($counts.unresolvedCandidates) unresolved candidates"; exitCode = 0 } } else { $verify = $null }
    [ordered]@{
        source = "git archive HEAD $($manifest.generatedFromCommit)"
        counts = $counts
        write = [ordered]@{ command = 'pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Write'; output = "manifest written: $($counts.records) records; 0 duplicates; $($counts.unresolvedCandidates) unresolved candidates"; exitCode = 0 }
        verify = $verify
        dydoCheck = [ordered]@{ command = 'dydo check'; output = $checkOutput; exitCode = $check.exitCode }
    }
}
function Get-ReviewSurface([object]$manifest) {
    $counts = @($manifest.records | Group-Object { $_.kind } | Sort-Object Name | ForEach-Object { "| $($_.Name) | $($_.Count) |" })
    $dispositionCounts = @($manifest.records | Group-Object { $_.proposedDisposition } | Sort-Object Name | ForEach-Object { "| $($_.Name) | $($_.Count) |" })
    $ambiguous = @($manifest.records | Where-Object { $_.proposedDisposition -in @('migrate-initiative', 'migrate-project', 'migrate-issue') } | Group-Object { "$($_.kind) | $($_.status) | $($_.proposedDisposition)" } | Sort-Object Name | ForEach-Object { "| $($_.Name) | $($_.Count) |" })
    $referenceCount = $manifest.provenance.counts.incomingReferences; $check = $manifest.provenance.dydoCheck
    $writeTranscript = $manifest.provenance.write.output; $verifyTranscript = if ($null -eq $manifest.provenance.verify) { 'not yet run' } else { $manifest.provenance.verify.output }
    $lines = [System.Collections.Generic.List[string]]::new()
    $prefix = @('---', 'area: project', 'type: context', '---', '', '# 3.0 PM record disposition review', '', "Generated from committed source $($manifest.generatedFromCommit). Record and reference discovery reads that Git archive; it does not read the working tree.", 'The source commit may predate the artifact commit, but must remain an ancestor of the checkout verified by this script.', 'The dydo check transcript is an acceptance-gate observation of the checkout, not migration-source input.', '', '## Inventory', '', '| Kind | Records |', '|---|---:|') + $counts + @('', "Total records: **$($manifest.records.Count)**. Incoming references: **$referenceCount**.", "Excluded candidates: **$($manifest.excludedCandidates.Count)**. Unresolved candidates: **$($manifest.unresolvedCandidates.Count)**.", '', '## Proposed dispositions', '', '| Disposition | Records |', '|---|---:|') + $dispositionCounts + @('', '## Ambiguous groups requiring human disposition', '', '| Kind | Status | Proposed disposition | Records |', '|---|---|---:|') + $ambiguous + @('', 'FutureFeatures are retain-normalize only. They remain repo-native ideas and are never promoted by this manifest.', '', '## Candidate review', '')
    foreach ($line in $prefix) { $lines.Add($line) }
    if ($manifest.excludedCandidates.Count -eq 0) { $lines.Add('No excluded candidates.') } else { foreach ($candidate in $manifest.excludedCandidates) { $lines.Add("- excluded: $($candidate.path) — $($candidate.exclusionReason)") } }
    if ($manifest.unresolvedCandidates.Count -eq 0) { $lines.Add('No unresolved candidates.') } else { foreach ($candidate in $manifest.unresolvedCandidates) { $lines.Add("- unresolved: $($candidate.path) — $($candidate.matchedSignature)") } }
    foreach ($line in @('', '## Human review checklist', '', '- [ ] Ratify every final disposition and record the human ruling evidence.', '- [ ] Keep every FutureFeature unpromoted.', '- [ ] Replace migration preview keys with Linear URLs and read-back evidence only after approved creation.', '- [ ] Record the exact pm-v2-final commit permalink before any historical removal.', '- [ ] Rewrite every incoming reference according to its effective final disposition.', '- [ ] Run the RequireRatified verification only after every row is ratified.', '', '## Gate and provenance transcript', '', "- source: $($manifest.provenance.source)", "- write: $writeTranscript", "- verify: $verifyTranscript", "- dydo check exit: $($check.exitCode)", "- dydo check summary: $($check.output)", '- command: pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Write', '- verification command: pwsh -NoProfile -File dydo/project/migrations/build-3.0-pm-manifest.ps1 -RepoRoot . -Verify')) { $lines.Add($line) }
    ($lines -join "`n") + "`n"
}
function Test-TargetAndEvidence([object]$record, [bool]$requireRatified) {
    if (-not (Test-PropertySet $record.target $targetFields) -or $record.target.kind -notin $targetKinds -or $record.target.value -isnot [string]) { return $false }
    $evidence = @($record.evidence); foreach ($item in $evidence) { if (-not (Test-PropertySet $item $evidenceFields) -or $item.kind -notin $evidenceKinds -or [string]::IsNullOrWhiteSpace($item.value)) { return $false } }
    if (@($evidence | Group-Object kind | Where-Object Count -gt 1).Count -ne 0) { return $false }
    $effective = if ($record.humanRatified) { $record.finalDisposition } else { $record.proposedDisposition }; $migration = $effective -in @('migrate-initiative', 'migrate-project', 'migrate-issue')
    if ($requireRatified -and -not $record.humanRatified) { return $false }
    if (-not $record.humanRatified) {
        if ($record.finalDisposition -ne $null -or $record.executionState -ne 'pending') { return $false }
        if ($migration) { return $record.target.kind -eq 'linear-preview-key' -and $record.target.value -match '^DYD-3-PREVIEW-[A-Z-]+-[A-Z0-9-]+$' -and $evidence.Count -eq 0 }
        if ($effective -in @('retain', 'retain-normalize')) { return $record.target.kind -eq 'retained-path' -and $record.target.value -eq $record.path -and $evidence.Count -eq 1 -and $evidence[0].kind -eq 'retained-path' -and $evidence[0].value -eq $record.path }
        if ([string]::IsNullOrWhiteSpace((Get-FreezeCommitUrl))) { return $record.target.kind -eq 'none' -and $record.target.value -eq '' -and $evidence.Count -eq 0 }
        return $record.target.kind -eq 'commit-permalink' -and (Test-FreezeCommitUrl $record.target.value) -and $evidence.Count -eq 1 -and $evidence[0].kind -eq 'freeze-commit' -and $evidence[0].value -eq $record.target.value
    }
    if (-not $requireRatified -or $record.finalDisposition -notin $dispositions) { return $false }
    $hasRuling = @($evidence | Where-Object kind -eq 'human-ruling').Count -eq 1
    if (-not $hasRuling) { return $false }
    if ($migration) {
        if ($record.executionState -eq 'pending') { return $record.target.kind -eq 'linear-preview-key' -and $record.target.value -match '^DYD-3-PREVIEW-[A-Z-]+-[A-Z0-9-]+$' -and $hasRuling -and $evidence.Count -eq 1 }
        return $record.executionState -eq 'applied' -and $record.target.kind -eq 'linear-url' -and $record.target.value -match '^https://linear\.app/' -and $hasRuling -and @($evidence | Where-Object kind -eq 'linear-readback').Count -eq 1 -and $evidence.Count -eq 2
    }
    if ($effective -in @('retain', 'retain-normalize')) { return $record.target.kind -eq 'retained-path' -and $record.target.value -match '^dydo/' -and @($evidence | Where-Object kind -eq 'retained-path').Count -eq 1 -and $evidence.Count -eq 2 -and @($evidence | Where-Object kind -eq 'retained-path').Value -eq $record.target.value }
    if ($effective -eq 'extract-then-remove') {
        $retained = @($evidence | Where-Object kind -eq 'retained-path')
        $freeze = @($evidence | Where-Object kind -eq 'freeze-commit')
        return $record.target.kind -eq 'retained-path' -and $retained.Count -eq 1 -and $retained[0].value -eq $record.target.value -and $freeze.Count -eq 1 -and (Test-FreezeCommitUrl $freeze[0].value) -and $evidence.Count -eq 3
    }
    if ($effective -in @('remove-historical', 'cancel-remove', 'drop-duplicate')) {
        if ([string]::IsNullOrWhiteSpace((Get-FreezeCommitUrl))) { return $record.executionState -eq 'pending' -and $record.target.kind -eq 'none' -and $record.target.value -eq '' -and $evidence.Count -eq 1 }
        $validFreeze = $record.target.kind -eq 'commit-permalink' -and (Test-FreezeCommitUrl $record.target.value); $replacementNamed = $effective -ne 'drop-duplicate' -or $record.reason -match '(dydo/.+\.md|https://linear\.app/)'
        return $validFreeze -and @($evidence | Where-Object kind -eq 'freeze-commit').Count -eq 1 -and @($evidence | Where-Object kind -eq 'freeze-commit').Value -eq $record.target.value -and $evidence.Count -eq 2 -and $replacementNamed
    }
    $false
}
function Test-Manifest([object]$manifest) {
    if (-not (Test-PropertySet $manifest $topFields) -or $manifest.schemaVersion -ne 1 -or $manifest.generatedFromCommit -notmatch '^[0-9a-f]{40}$') { throw 'Manifest top-level schema is invalid.' }
    if ($null -eq $manifest.provenance) { throw 'Manifest provenance is missing.' }
    $expected = Get-Inventory $manifest; $expectedPaths = @($expected.records | ForEach-Object path | Sort-Object); $actualPaths = @($manifest.records | ForEach-Object path | Sort-Object)
    $missing = @($expectedPaths | Where-Object { $_ -notin $actualPaths }).Count + @($actualPaths | Where-Object { $_ -notin $expectedPaths }).Count; $duplicates = @($actualPaths | Group-Object | Where-Object Count -gt 1).Count; $invalidRows = 0; $invalidReferences = 0; $expectedByPath = @{}
    foreach ($record in $expected.records) { $expectedByPath[$record.path] = $record }
    $sourcePathSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase); foreach ($path in Get-HeadSourcePaths) { [void]$sourcePathSet.Add($path) }
    foreach ($record in @($manifest.records)) {
        if (-not (Test-PropertySet $record $recordFields) -or $record.kind -notin $legacyFolders.Values -or $record.executionState -notin @('pending', 'applied') -or $record.proposedDisposition -notin $dispositions -or $record.humanRatified -isnot [bool] -or ($null -ne $record.status -and $record.status -isnot [string]) -or $record.outsideCanonicalFolder -isnot [bool] -or [string]::IsNullOrWhiteSpace($record.reason)) { $invalidRows++; continue }
        $expectedRecord = $expectedByPath[$record.path]
        if ($null -eq $expectedRecord -or $record.kind -ne $expectedRecord.kind -or $record.status -ne $expectedRecord.status -or $record.outsideCanonicalFolder -ne $expectedRecord.outsideCanonicalFolder -or $record.proposedDisposition -ne $expectedRecord.proposedDisposition -or -not (Test-TargetAndEvidence $record $RequireRatified)) { $invalidRows++; continue }
        $expectedReferences = @($expectedRecord.incomingReferences | ForEach-Object { Get-ReferenceKey $_ } | Sort-Object); $actualReferences = @($record.incomingReferences | ForEach-Object { Get-ReferenceKey $_ } | Sort-Object)
        if (@($expectedReferences).Count -ne @($actualReferences).Count -or (@($expectedReferences) -join "`n") -ne (@($actualReferences) -join "`n")) { $invalidReferences++ }
        foreach ($reference in @($record.incomingReferences)) { if (-not (Test-PropertySet $reference $referenceFields) -or -not $sourcePathSet.Contains($reference.sourcePath) -or $reference.line -isnot [long] -or $reference.line -lt 1 -or [string]::IsNullOrWhiteSpace($reference.rawTarget) -or $reference.resolution -notin @('unchanged', 'rewrite-linear', 'rewrite-retained', 'rewrite-commit-permalink') -or $reference.resolution -ne (Get-ReferenceResolution $record)) { $invalidReferences++ } }
    }
    $previewKeys = [System.Collections.Generic.List[string]]::new()
    foreach ($record in @($manifest.records)) {
        $effective = if ($record.humanRatified) { $record.finalDisposition } else { $record.proposedDisposition }
        if ($record.executionState -eq 'pending' -and $effective -in @('migrate-initiative', 'migrate-project', 'migrate-issue')) { $previewKeys.Add($record.target.value) }
    }
    if (@($previewKeys | Group-Object | Where-Object Count -gt 1).Count -ne 0) { $invalidRows++ }
    $candidateErrors = 0
    foreach ($candidate in @($manifest.excludedCandidates)) { if (-not (Test-PropertySet $candidate $excludedFields) -or [string]::IsNullOrWhiteSpace($candidate.path) -or [string]::IsNullOrWhiteSpace($candidate.matchedSignature) -or [string]::IsNullOrWhiteSpace($candidate.exclusionReason) -or $candidate.humanRatified -ne $true) { $candidateErrors++ } }
    foreach ($candidate in @($manifest.unresolvedCandidates)) { if (-not (Test-PropertySet $candidate $unresolvedFields) -or [string]::IsNullOrWhiteSpace($candidate.path) -or [string]::IsNullOrWhiteSpace($candidate.matchedSignature)) { $candidateErrors++ } }
    $candidateKeys = { param($items) @($items | ForEach-Object { "$($_.path):$($_.matchedSignature)" } | Sort-Object) }
    $expectedExcluded = & $candidateKeys @($expected.excludedCandidates)
    $actualExcluded = & $candidateKeys @($manifest.excludedCandidates)
    $expectedUnresolved = & $candidateKeys @($expected.unresolvedCandidates)
    $actualUnresolved = & $candidateKeys @($manifest.unresolvedCandidates)
    if (@($expectedExcluded).Count -ne @($actualExcluded).Count -or @($expectedUnresolved).Count -ne @($actualUnresolved).Count -or (@($expectedExcluded) -join "`n") -ne (@($actualExcluded) -join "`n") -or (@($expectedUnresolved) -join "`n") -ne (@($actualUnresolved) -join "`n")) { $candidateErrors++ }
    $unresolved = @($manifest.unresolvedCandidates).Count
    $expectedProvenance = Get-Provenance $manifest ($null -ne $manifest.provenance.verify) $manifest.provenance.dydoCheck
    $actualProvenanceJson = $manifest.provenance | ConvertTo-Json -Depth 10 -Compress
    $expectedProvenanceJson = $expectedProvenance | ConvertTo-Json -Depth 10 -Compress
    $provenanceError = $actualProvenanceJson -ne $expectedProvenanceJson
    if ($provenanceError) { $candidateErrors++ }
    if ($missing -ne 0 -or $duplicates -ne 0 -or $invalidRows -ne 0 -or $invalidReferences -ne 0 -or $candidateErrors -ne 0 -or $unresolved -ne 0) { throw "manifest verification failed: $($manifest.records.Count) records; $missing missing; $duplicates duplicates; $invalidReferences invalid references; $unresolved unresolved candidates; $invalidRows invalid rows; $candidateErrors candidate errors; provenanceError=$provenanceError" }
    if ($RequireRatified) { Write-Output "ratification verified: $($manifest.records.Count)/$($manifest.records.Count); 0 missing final dispositions; 0 target/evidence violations" } else { Write-Output "manifest verified: $($manifest.records.Count) records; 0 missing; 0 duplicates; 0 invalid references; 0 unresolved candidates" }
}

$script:sourceCommit = Get-HeadCommit
$existing = Read-ExistingManifest
if ($Write) {
    $manifest = Get-Inventory $existing
    $manifest.provenance = Get-Provenance $manifest $false ([pscustomobject]@{ exitCode = 0; summary = 'not yet run' })
    [IO.File]::WriteAllText($jsonPath, (($manifest | ConvertTo-Json -Depth 20).Replace("`r`n", "`n")))
    [IO.File]::WriteAllText($markdownPath, (Get-ReviewSurface $manifest))
    $manifest.provenance = Get-Provenance $manifest $false (Get-CheckTranscript)
    [IO.File]::WriteAllText($jsonPath, (($manifest | ConvertTo-Json -Depth 20).Replace("`r`n", "`n")))
    [IO.File]::WriteAllText($markdownPath, (Get-ReviewSurface $manifest))
    Write-Output "manifest written: $($manifest.records.Count) records; 0 duplicates; $($manifest.unresolvedCandidates.Count) unresolved candidates"
    exit 0
}
if ($null -eq $existing -or -not (Test-Path -LiteralPath $markdownPath)) { throw 'Manifest artifacts are missing. Run with -Write first.' }
Test-SourceCommit $existing.generatedFromCommit
Set-SourceCommit $existing.generatedFromCommit
Test-Manifest $existing
$expectedMarkdown = Get-ReviewSurface $existing
$actualMarkdown = Get-Content -Raw -LiteralPath $markdownPath
if ($actualMarkdown.Replace("`r`n", "`n") -ne $expectedMarkdown) { throw 'Manifest Markdown does not match the deterministic review surface. Run with -Write again.' }
$existing.provenance = Get-Provenance $existing $true (Get-CheckTranscript)
[IO.File]::WriteAllText($jsonPath, (($existing | ConvertTo-Json -Depth 20).Replace("`r`n", "`n")))
[IO.File]::WriteAllText($markdownPath, (Get-ReviewSurface $existing))
