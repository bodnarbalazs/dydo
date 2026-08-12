---
title: Regenerate runtimes and prove the release candidate
sprint: lean-wayfinder-adoption
seq: 4
status: done
blocked-by: [lean-wayfinder-adoption-3-public-release]
area: general
type: context
---

# Slice 4 — Regenerate runtimes and prove the release candidate

Compile the canonical sources once and prove the complete release candidate without absorbing
unrelated worktree changes.

## Spec fragment

Compile the canonical templates once, reconcile the compiler-owned runtime artifacts, and run the
full release-candidate gates without modifying unrelated dirty work.

Acceptance: existing template-backed generated changes survive, new skills appear for Claude and
Codex only, generated artifacts match their templates and use LF, and every full gate passes.

## Implementation detail

Before sync, compare every currently modified generated role artifact with its root
`Templates/mode-*.template.md` source. If a generated difference is not represented in the source,
stop and report it; do not overwrite user work. Once sources cover the intended state, run:

```powershell
dydo sync
```

Review the entire generated diff. Expected new outputs:

- `.claude/skills/wayfinder/SKILL.md`, `.agents/skills/wayfinder/SKILL.md`
- `.claude/skills/grilling/SKILL.md`, `.agents/skills/grilling/SKILL.md`
- `.claude/skills/bro/SKILL.md`, `.agents/skills/bro/SKILL.md`

No `.claude/agents/{wayfinder,grilling,bro}.md` or
`.codex/agents/{wayfinder,grilling,bro}.toml` may exist. Existing changed generated roles must be
deterministic products of their root templates.

Do not regenerate PM hubs in this release. A read-only probe of `dydo fix dydo/project` at planning
time showed 37 unrelated stale hub changes and incorrectly created `project/tasks/_index.md`; manual
edits would violate the generated-file ownership marker. The Sprint and Slice records remain
directly addressable by path, while generator/task-index cleanup is separate work. This bounded
exception is preferable to absorbing unrelated index churn into a lean prompt release.

Run the full isolated tests, coverage gap check, documentation check, and version smoke. Record
results in the Sprint gate and set it to `audit`. Do not stage or modify unrelated task files,
issue 0308, or the unrelated handoff file.

Build and smoke-install the exact release candidate outside the source checkout so embedded
resources—not development-mode `Templates/` discovery—are exercised. Use the previously absent
fixed paths `C:\tmp\dydo-v226-package`, `C:\tmp\dydo-v226-tool`, and
`C:\tmp\dydo-v226-rc-smoke`; if any exists at execution time, stop and choose a new explicit
task-specific set in the Sprint result before writing.

## Out of scope for this slice

Source prompt/docs/license changes, public publication, downstream post-release update, or cleanup
of unrelated worktree state.

## Gate

```powershell
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
dydo check
dotnet build DynaDocs.csproj -c Release --nologo
dotnet pack DynaDocs.csproj -c Release -p:Version=2.2.6 -o C:\tmp\dydo-v226-package --no-restore
dotnet tool install --tool-path C:\tmp\dydo-v226-tool --add-source C:\tmp\dydo-v226-package dydo --version 2.2.6
$rcVersion = (& C:\tmp\dydo-v226-tool\dydo.exe version).Trim()
if ($rcVersion -ne 'dydo version 2.2.6') { throw "RC version mismatch: $rcVersion" }
New-Item -ItemType Directory -Path C:\tmp\dydo-v226-rc-smoke -ErrorAction Stop
Push-Location C:\tmp\dydo-v226-rc-smoke
try {
  & C:\tmp\dydo-v226-tool\dydo.exe init all
  if ($LASTEXITCODE -ne 0) { throw 'Packed RC init failed.' }
  & C:\tmp\dydo-v226-tool\dydo.exe sync
  if ($LASTEXITCODE -ne 0) { throw 'Packed RC sync failed.' }
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
  foreach ($path in $forbidden) { if (Test-Path -LiteralPath $path) { throw "Forbidden $path" } }
}
finally { Pop-Location }
git diff --check -- .agents .claude .codex dydo/project/sprints/lean-wayfinder-adoption.md dydo/project/slices/lean-wayfinder-adoption-4-generated-and-e2e.md dydo/project/tasks/publish-and-adopt-dydo-v2-2-6.md
```

The final diff check is deliberately path-scoped: the five protected pre-existing dirty Task files
contain unrelated trailing whitespace and are forbidden to this Sprint. Their byte integrity is
proved separately by the release Task's SHA256 manifest; they must not be “fixed” here.

## Result

PASS — all 16 pre-existing generated changes matched compiler output from the canonical
pre-Slice-2 sources at `68b311d0`, so no generated user work was lost. One repository `dydo sync`
then produced 24 real changed artifacts, all byte-identical to an isolated current-source compile
and LF-only. The six required Wayfinder/Grilling/Bro skills exist and all six forbidden native
agent definitions are absent.

The full isolated suite passed (2,538 passed, 10 skipped), coverage passed 131/131 modules,
`dydo check` reported 0 errors and 13 known orphan warnings, the Release build and 2.2.6 pack
passed, and the packed tool initialized and synced `C:\tmp\dydo-v226-rc-smoke` with all artifact
assertions green. The revised Slice-owned diff check passed; protected unrelated files were not
modified.
