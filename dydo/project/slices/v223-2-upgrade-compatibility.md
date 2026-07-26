---
title: v223-2 Upgrade Compatibility
blocked-by: v223-1-pending-patch
due:
needs-human: false
priority: High
sprint: v2-2-3-upgrade-compatibility
status: done
work-type: bugfix
area: backend
type: context
---

# v223-2 Upgrade Compatibility

Make integration-aware sync backward compatible, make inquisitor reproducible from shipped sources, retire stale sprint-auditor outputs without touching user siblings, and correct must-read documentation.

## Task

1. Finish the existing issue-0300 `Commands/SyncCommand.cs` behavior and `DynaDocs.Tests/Commands/SyncCommandTests.cs` coverage: emit only recorded integrations, but emit both when neither Claude nor Codex is recorded.
2. Add `Templates/mode-inquisitor.template.md` with exact frontmatter:
   - `mode: inquisitor`
   - `description: Campaign-end QA sweeper — audits landed work through one lens (correctness, test-coverage gaps, security, dead code, or doc drift), or adversarially verifies a single finding, returning structured results.`
   - `emit: agent`
   - `read-only: true`
   Its `## Must-Reads` lists `about.md`, `architecture.md`, and `coding-standards.md`. Its methodology body is the body of the existing `.agents/skills/inquisitor/SKILL.md` (frontmatter excluded).
3. Add allowlisted cleanup for absent `sprint-auditor` generated files:
   - `.claude/agents/sprint-auditor.md`
   - `.claude/skills/sprint-auditor/SKILL.md`
   - `.codex/agents/sprint-auditor.toml`
   - `.agents/skills/sprint-auditor/SKILL.md`
   Remove a parent directory only when it becomes empty. Generated filenames are compiler-owned and are deleted regardless of edits; unrelated sibling files/directories are user-owned and preserved.
4. Prove cleanup preserves all generated artifacts when a project-local `mode-sprint-auditor.template.md` exists, and separately prove an unrelated sibling file survives retired-role cleanup.
5. Remove the obsolete `must-read` frontmatter/enforcement claims from both the shipped writing-docs template and this repository's generated copy.
6. Run `dotnet run --project DynaDocs.csproj -- sync` and verify these four outputs, with no diff on an identical second run:
   - `.claude/agents/inquisitor.md`
   - `.claude/skills/inquisitor/SKILL.md`
   - `.codex/agents/inquisitor.toml`
   - `.agents/skills/inquisitor/SKILL.md`

## Files

- `Commands/SyncCommand.cs`
- `DynaDocs.Tests/Commands/SyncCommandTests.cs`
- `DynaDocs.Tests/Integration/TemplateOverrideTests.cs`
- `DynaDocs.Tests/Services/RoleDefinitionServiceTests.cs` — change `DiscoverRoles_FindsAllShippedRoles` from asserting inquisitor is retired to asserting it is shipped; update the read-only-base-role assertion to include inquisitor
- `Templates/mode-inquisitor.template.md`
- `Templates/writing-docs.template.md`
- `dydo/reference/writing-docs.md`
- `.claude/agents/inquisitor.md`
- `.claude/skills/inquisitor/SKILL.md`
- `.codex/agents/inquisitor.toml`
- `.agents/skills/inquisitor/SKILL.md`

## Success Criteria

- `py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~SyncCommandTests|FullyQualifiedName~RoleDefinitionServiceTests|FullyQualifiedName~TemplateOverrideTests"` passes.
- A clean test project compiles inquisitor for both supported runtimes.
- Known stale sprint-auditor outputs disappear only when no current role definition owns them; unrelated sibling files survive.
- Writing guidance contains no `must-read` enforcement claim.
- Source-built sync produces the four enumerated artifacts and a second identical run is a no-op.
