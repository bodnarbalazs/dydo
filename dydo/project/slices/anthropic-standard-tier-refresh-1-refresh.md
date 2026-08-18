---
title: Refresh Anthropic Standard Tier
sprint: anthropic-standard-tier-refresh
seq: 1
status: done
area: platform
type: context
---

# Slice 1 — Refresh Anthropic Standard Tier

Align the shipped standard-tier binding, local configuration, generated agents, and focused tests.

## Spec fragment

Change the shipped and repository-local Anthropic standard-tier binding from
`claude-opus-4-8` to `claude-opus-5`, and align the three generated standard-tier Claude worker
agents plus focused tests. The Slice is accepted when all six owned paths are aligned, historical
Records are untouched, and every gate passes.

## Implementation detail

Touch only the following implementation/configuration paths:

- `Services/ConfigFactory.cs`
- `DynaDocs.Tests/Services/ModelCapServiceTests.cs`
- `dydo.json`
- `.claude/agents/code-writer.md`
- `.claude/agents/docs-writer.md`
- `.claude/agents/test-writer.md`

1. In `ConfigFactory.CreateDefaultModels()`, change only the Anthropic `standard` tier value to
   `claude-opus-5`; preserve strong, light, OpenAI, role, effort, and fallback values.
2. In the current repository's `dydo.json`, change only `models.tiers.anthropic.standard` to the
   same identifier. Preserve all unrelated pre-existing dirty content and formatting.
3. In each of the three generated standard-tier Claude agent definitions, change only the
   frontmatter `model` value to the new standard-tier identifier. Leave reviewer and inquisitor
   definitions unchanged.
4. In `ModelCapServiceTests`, update the stale standard-tier expectation and the two explicit
   fallback scenarios to use the new identifier. Do not change model-cap behavior.
5. Search active source, configuration, generated agents, and tests for the old identifier and
   require zero matches. Do not alter historical Records returned by a repository-wide search.

## Out of scope for this slice

- Any path outside the six implementation/configuration paths above, except these Sprint and Slice
  Records.
- Model resolution logic, tier-role mappings, migrations, workflows, templates, or documentation.
- Rewriting historical provenance or past decisions.

## Gate

Run in order and require every command to pass:

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~ModelCapServiceTests
$stale_refs = rg -n -F 'claude-opus-4-8' Services/ConfigFactory.cs DynaDocs.Tests/Services/ModelCapServiceTests.cs dydo.json .claude/agents; if ($LASTEXITCODE -eq 0) { $stale_refs; exit 1 }; if ($LASTEXITCODE -gt 1) { exit $LASTEXITCODE }
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py
dydo check dydo/project/sprints/anthropic-standard-tier-refresh.md
dydo check dydo/project/slices/anthropic-standard-tier-refresh-1-refresh.md
git diff --check -- Services/ConfigFactory.cs DynaDocs.Tests/Services/ModelCapServiceTests.cs dydo.json .claude/agents/code-writer.md .claude/agents/docs-writer.md .claude/agents/test-writer.md dydo/project/sprints/anthropic-standard-tier-refresh.md dydo/project/slices/anthropic-standard-tier-refresh-1-refresh.md
```

## Completion evidence

On 2026-08-18 the focused `ModelCapServiceTests` suite passed 28/28 after first proving the old shipped default red. The active-scope stale-reference search returned zero matches; the existing coverage artifact records full line coverage for `ConfigFactory`, and the build, Record checks, and scoped diff check passed.
