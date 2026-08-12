---
title: Publish and adopt dydo v2.2.6
status: backlog
priority: Urgent
area: project
type: context
---

# Publish and adopt dydo v2.2.6

Publish the audited and sealed Lean Wayfinder Sprint, allow registry indexing, then prove the
installed package can initialize a clean project and update this repository as a downstream
consumer. This Task creates no source commit.

## Preconditions

- The merged implementation audit for `lean-wayfinder-adoption` is PASS and names its audited SHA.
- The follow-up release-seal reviewer is PASS and names the exact `SEALED_SHA`; it has verified that
  the only change after the implementation-audit SHA is the Sprint verdict/status metadata.
- Slices 1–4 are done, local branch is `master`, and local `master`/`HEAD` equal `SEALED_SHA`.
- Global `dydo version` reports 2.2.5 before replacement.
- `C:\tmp\dydo-v226-installed-smoke` does not exist. If it exists, stop and record a new literal
  empty path in this Task; never overwrite or delete an unknown directory.

## Procedure

1. Substitute the release-seal reviewer's literal 40-character SHA once below, then prove that the
   sealed tree is local `master`, the index contains nothing staged, all release-owned paths are
   clean, and the seven known unrelated paths are the only dirty paths. Snapshot those seven files
   to an external SHA256 manifest:

   ```powershell
   $sealedSha = '<SEALED_SHA_FROM_RELEASE_SEAL>'
   if ($sealedSha -notmatch '^[0-9a-f]{40}$') { throw 'SEALED_SHA must be a full commit SHA.' }
   if ((git branch --show-current).Trim() -ne 'master') { throw 'Release must run on master.' }
   if ((git rev-parse HEAD).Trim() -ne $sealedSha) { throw 'HEAD is not SEALED_SHA.' }
   if ((git rev-parse refs/heads/master).Trim() -ne $sealedSha) { throw 'master is not SEALED_SHA.' }
   git diff --cached --quiet
   if ($LASTEXITCODE -ne 0) { throw 'Index is not clean.' }

   $unrelatedPaths = @(
     'HANDOFF-fix-command-failure.md',
     'dydo/project/issues/0308-no-path-re-establishes-codex-hook-trust-since-dr-041-removed-the-dispatch-self-r.md',
     'dydo/project/tasks/swarm-0291-chunking.md',
     'dydo/project/tasks/swarm-0293-displaynames.md',
     'dydo/project/tasks/swarm-0293-tiers.md',
     'dydo/project/tasks/v206-campaign-inquisition-2.md',
     'dydo/project/tasks/watchdog-autostart-lease.md'
   )
   $dirtyPaths = @(git status --porcelain=v1 --untracked-files=all | ForEach-Object { $_.Substring(3) })
   $unexpected = @($dirtyPaths | Where-Object { $_ -notin $unrelatedPaths })
   $missing = @($unrelatedPaths | Where-Object { $_ -notin $dirtyPaths })
   if ($unexpected.Count -or $missing.Count) {
     throw "Dirty-path mismatch. Unexpected: $unexpected; missing: $missing"
   }
   $unrelatedBefore = @{}
   foreach ($path in $unrelatedPaths) {
     if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing unrelated file: $path" }
     $unrelatedBefore[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
   }
   $hashManifest = Join-Path ([IO.Path]::GetTempPath()) 'dydo-v226-unrelated-sha256.json'
   $unrelatedBefore | ConvertTo-Json | Set-Content -LiteralPath $hashManifest -Encoding utf8
   ```

2. Refresh refs and prove the local/remote/tag preconditions. A pre-existing local or remote
   `v2.2.6` is a hard stop; remote lookup exit 2 is the required “absent” result:

   ```powershell
   git fetch origin master --tags
   if ($LASTEXITCODE -ne 0) { throw 'git fetch failed.' }
   if ((git rev-parse HEAD).Trim() -ne $sealedSha) { throw 'HEAD changed during fetch.' }
   git merge-base --is-ancestor origin/master $sealedSha
   if ($LASTEXITCODE -ne 0) { throw 'SEALED_SHA does not descend from origin/master.' }
   if (git tag --list v2.2.6) { throw 'Local v2.2.6 already exists.' }
   git ls-remote --exit-code --tags origin refs/tags/v2.2.6
   if ($LASTEXITCODE -ne 2) { throw 'Remote tag exists or lookup failed unexpectedly.' }
   gh auth status
   if ($LASTEXITCODE -ne 0) { throw 'GitHub authentication failed.' }
   if (!(Select-String -Path .github/workflows/release.yml -Pattern "'v\*'" -Quiet)) {
     throw 'Release workflow no longer triggers on v*.'
   }
   $projectAtSeal = git show "${sealedSha}:DynaDocs.csproj"
   $npmAtSeal = git show "${sealedSha}:npm/package.json"
   if ($projectAtSeal -notmatch '<Version>2\.2\.6</Version>' -or
       $projectAtSeal -notmatch '<PackageLicenseExpression>MIT</PackageLicenseExpression>') {
     throw 'Sealed NuGet manifest is not 2.2.6/MIT.'
   }
   if ($npmAtSeal -notmatch '"version"\s*:\s*"2\.2\.6"' -or
       $npmAtSeal -notmatch '"license"\s*:\s*"MIT"') {
     throw 'Sealed npm manifest is not 2.2.6/MIT.'
   }
   ```

3. Push local `master`, prove the remote branch equals the sealed SHA, create the annotated tag on
   that SHA explicitly, push it, and locate exactly one tag-triggered release run with a bounded
   five-minute poll:

   ```powershell
   git push origin master
   if ($LASTEXITCODE -ne 0) { throw 'master push failed.' }
   $remoteMaster = (git ls-remote origin refs/heads/master).Split("`t")[0]
   if ($remoteMaster -ne $sealedSha) { throw 'Remote master is not SEALED_SHA.' }
   git tag -a v2.2.6 $sealedSha -m 'dydo 2.2.6'
   if ((git rev-list -n 1 v2.2.6).Trim() -ne $sealedSha) { throw 'Tag does not peel to SEALED_SHA.' }
   git push origin refs/tags/v2.2.6
   if ($LASTEXITCODE -ne 0) { throw 'Tag push failed.' }
   $tagPushUtcPath = Join-Path ([IO.Path]::GetTempPath()) 'dydo-v226-tag-push-utc.txt'
   [DateTime]::UtcNow.ToString('o') | Set-Content -LiteralPath $tagPushUtcPath -Encoding ascii

   $releaseRun = $null
   for ($attempt = 0; $attempt -lt 30; $attempt++) {
     $rows = @(gh run list --workflow release.yml --branch v2.2.6 --event push --limit 2 `
       --json databaseId,headBranch,event,status,conclusion | ConvertFrom-Json)
     if ($rows.Count -gt 1) { throw 'Multiple v2.2.6 release runs found.' }
     if ($rows.Count -eq 1) { $releaseRun = $rows[0]; break }
     Start-Sleep -Seconds 10
   }
   if ($null -eq $releaseRun) { throw 'No v2.2.6 release run appeared within five minutes.' }
   if ($releaseRun.headBranch -ne 'v2.2.6' -or $releaseRun.event -ne 'push') {
     throw 'Selected workflow run is not the v2.2.6 tag push.'
   }
   gh run watch $releaseRun.databaseId --exit-status
   if ($LASTEXITCODE -ne 0) { throw 'Release workflow failed.' }
   ```

   After tag push, source correction means stop and plan 2.2.7; never move the public tag.

4. Wait until at least 15 minutes have elapsed before installation. Run this block once per minute;
   after each run, report its output to the human before invoking it again. Each invocation sleeps
   at most 60 seconds, and the loop ends only when it prints `Registry wait complete`:

   ```powershell
   $tagPushUtc = [DateTime]::Parse((Get-Content -LiteralPath $tagPushUtcPath -Raw).Trim()).ToUniversalTime()
   $remaining = [TimeSpan]::FromMinutes(15) - ([DateTime]::UtcNow - $tagPushUtc)
   if ($remaining.TotalSeconds -le 0) { Write-Output 'Registry wait complete' }
   else {
     Write-Output ("Registry wait remaining: {0:n0} seconds" -f $remaining.TotalSeconds)
     Start-Sleep -Seconds ([Math]::Min(60, [Math]::Ceiling($remaining.TotalSeconds)))
   }
   ```

5. Uninstall the known 2.2.5 global tool, then perform the user's requested kill/install sequence
   and assert the installed version. `taskkill` exit 128/no matching process is acceptable; other
   failures are not:

   ```powershell
   $tagPushUtc = [DateTime]::Parse((Get-Content -LiteralPath $tagPushUtcPath -Raw).Trim()).ToUniversalTime()
   if (([DateTime]::UtcNow - $tagPushUtc).TotalMinutes -lt 15) { throw '15-minute registry wait is incomplete.' }
   dotnet tool uninstall -g dydo
   if ($LASTEXITCODE -ne 0) { throw 'Could not uninstall global dydo 2.2.5.' }
   taskkill /im dydo.exe /f
   if ($LASTEXITCODE -notin @(0, 128)) { throw 'Unexpected taskkill failure.' }
   dotnet tool install -g dydo
   if ($LASTEXITCODE -ne 0) {
     dotnet tool install -g dydo --version 2.2.6
     if ($LASTEXITCODE -ne 0) { throw 'Could not install published dydo 2.2.6.' }
   }
   $installedVersion = (dydo version).Trim()
   if ($installedVersion -ne 'dydo version 2.2.6') { throw "Unexpected dydo version: $installedVersion" }
   ```

6. Prove the installed package's embedded resources outside the source checkout. Every expected
   skill must exist and every forbidden native agent definition must be absent:

   ```powershell
   New-Item -ItemType Directory -Path C:\tmp\dydo-v226-installed-smoke -ErrorAction Stop
   Push-Location C:\tmp\dydo-v226-installed-smoke
   try {
     dydo init all
     if ($LASTEXITCODE -ne 0) { throw 'Installed dydo init failed.' }
     dydo sync
     if ($LASTEXITCODE -ne 0) { throw 'Installed dydo sync failed.' }
     $smokeVersion = (dydo version).Trim()
     if ($smokeVersion -ne 'dydo version 2.2.6') { throw "Smoke version mismatch: $smokeVersion" }
     $required = @(
       '.claude/skills/wayfinder/SKILL.md', '.claude/skills/grilling/SKILL.md',
       '.claude/skills/bro/SKILL.md', '.agents/skills/wayfinder/SKILL.md',
       '.agents/skills/grilling/SKILL.md', '.agents/skills/bro/SKILL.md'
     )
     $forbidden = @(
       '.claude/agents/wayfinder.md', '.claude/agents/grilling.md', '.claude/agents/bro.md',
       '.codex/agents/wayfinder.toml', '.codex/agents/grilling.toml', '.codex/agents/bro.toml'
     )
     foreach ($path in $required) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing $path" } }
     foreach ($path in $forbidden) { if (Test-Path -LiteralPath $path) { throw "Forbidden artifact $path" } }
   }
   finally { Pop-Location }
   ```

7. Back in this repository, update installed framework copies before compiling them. Assert every
   expected path, run the framework check, and prove the seven unrelated files still match the
   external SHA256 manifest exactly:

   ```powershell
   dydo template update --diff
   if ($LASTEXITCODE -ne 0) { throw 'Template diff failed.' }
   dydo template update
   if ($LASTEXITCODE -ne 0) { throw 'Template update failed.' }
   dydo sync
   if ($LASTEXITCODE -ne 0) { throw 'Sync failed.' }
   dydo check
   if ($LASTEXITCODE -ne 0) { throw 'dydo check failed.' }
   $requiredProjectPaths = @(
     'dydo/_system/templates/mode-wayfinder.template.md',
     'dydo/_system/templates/mode-grilling.template.md',
     'dydo/_system/templates/mode-bro.template.md',
     '.claude/skills/wayfinder/SKILL.md', '.claude/skills/grilling/SKILL.md',
     '.claude/skills/bro/SKILL.md', '.agents/skills/wayfinder/SKILL.md',
     '.agents/skills/grilling/SKILL.md', '.agents/skills/bro/SKILL.md'
   )
   foreach ($path in $requiredProjectPaths) {
     if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing project artifact: $path" }
   }
   $unrelatedAfter = Get-Content -LiteralPath $hashManifest -Raw | ConvertFrom-Json -AsHashtable
   foreach ($path in $unrelatedPaths) {
     if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Unrelated file disappeared: $path" }
     $afterHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
     if ($afterHash -ne $unrelatedAfter[$path]) { throw "Unrelated file changed: $path" }
   }
   ```

   Report expected post-tag installed-template/config/generated changes separately; never amend or
   fold them into v2.2.6.

## Success criteria

- Remote `master` and annotated `v2.2.6` peel to the exact release-sealed SHA; release workflow
  succeeds.
- At least 15 minutes elapse before global installation; global and clean-project versions are
  2.2.6; six embedded skills exist and six forbidden agents do not.
- This project receives the templates through update, recompiles through sync, passes `dydo check`,
  and preserves every unrelated dirty-file hash.

## Failure rule

Never move published `v2.2.6`. Source corrections become 2.2.7; registry-only failures may be
retried against the immutable tag.
