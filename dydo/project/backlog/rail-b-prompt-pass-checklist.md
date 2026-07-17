---
area: project
type: context
name: rail-b-prompt-pass-checklist
status: open
created: 2026-07-17
created-by: Adele (Fable)
---

# Rail B — prompt-engineering pass checklist (balazs)

The goal per balazs (2026-07-17): remove/rework ALL prompt files — skills, agents,
templates — killing every trace of the old workflow (claim identity, dispatch, inbox,
wait). The new version emphasizes **skills**; **progressive disclosure still holds**:
`CLAUDE.md / AGENTS.md → dydo/index.md → understand/ → guides/ + reference/`, now
anchored on skills instead of the claim ceremony. Every file below was grep-verified to
contain claim-era wording (claim/whoami/dispatch/inbox/wait/workflow.md/roster).

## 1. The entry point — your new headline file

- [ ] **`Templates/claude-md.template.md`** ← THIS generates both CLAUDE.md and
      AGENTS.md (one template, `{{PROJECT_NAME}}` placeholder). Just promoted from a
      4-line inline C# string so you can author it as markdown — make it as verbose as
      you want; `dydo init` materializes it (WriteIfNotExists — existing projects keep
      their copy).
- [ ] This repo's own `CLAUDE.md` (and root `AGENTS.md` if present) — edit directly;
      init won't overwrite them.

## 2. Compiled outputs — nuke & regenerate (don't hand-edit)

- [ ] `.claude/agents/` (6 files) + `.claude/skills/` (10 dirs) + `.codex/agents/` +
      `.agents/skills/` — all compiled by `dydo sync` from the sources in §3. Workflow:
      rework sources → delete outputs → `dydo sync`.
- [ ] ⚠ Before deleting `.claude/` wholesale, separate NON-dydo-compiled assets:
      hand-authored workflows/scripts (run-sprint, inquisition harnesses), plugin
      skills, `settings.json`/hooks, launch.json. Only the 10 role skills + 6 role
      agents are sync-owned.

## 3. Compiler sources — the actual prompt engineering

- [ ] `Templates/mode-*.template.md` ×9 (chief-of-staff, co-thinker, code-writer,
      docs-writer, orchestrator, planner, reviewer, sprint-auditor, test-writer) — the
      methodology bodies `dydo sync` compiles into skills/agents. All carry claim-era
      prose and `{{AGENT_NAME}}` placeholders (the roster is gone — decide the
      placeholder's fate in the sync pipeline or drop it from templates).
- [ ] `dydo/_system/roles/*.role.json` — prose fields only: `description`,
      `mustReads` lists (several point at docs you may delete below), `denialHint`
      remnants. Tier/tools/paths config is fine. (Constraints arrays already removed.)
- [ ] `Templates/template-additions-readme.md` — explains the template override system
      with claim-era examples.

## 4. Docs about deleted machinery — review, then likely DELETE outright

- [ ] `dydo/understand/agent-lifecycle.md` — claim→role→release lifecycle. Dead.
- [ ] `dydo/understand/dispatch-and-messaging.md` — dispatch/inbox/msg. Dead.
- [ ] `dydo/understand/multi-agent-workflows.md` — check: pre-2.0 fleet narrative vs
      salvageable native-workflow content.
- [ ] `dydo/understand/work-model.md` — Tier-1/Tier-2 + dispatch framing; salvage the
      tiers doctrine, kill the dispatch plumbing.
- [ ] `dydo/guides/agent-general-wait.md` — the wait ceremony. Dead.
- [ ] `dydo/guides/writing-good-briefs.md` — dispatch briefs; salvageable as
      "writing good skill/subagent briefs".
- [ ] `dydo/guides/migrating-dydo-1x-to-2x.md` — decide: keep for downstream (LC)
      migration or supersede with a 2.x→2.1 note.
- [ ] `dydo/reference/roles/inquisitor.md` — the retired role (inquisitor lives on as
      a workflow-spawned agent, not a role); align or delete.

## 5. Keep + reword (tone pass, kill claim-era vocabulary)

Front door & core:
- [ ] `dydo/index.md` + `Templates/index.template.md` (mechanically fixed; needs your
      voice + the skills emphasis)
- [ ] `dydo/glossary.md` — defines claim/dispatch/inbox-era terms
- [ ] `dydo/reference/about-dynadocs.md` + `Templates/about-dynadocs.template.md`
      (byte-identical pair!) + `README.md` — onboarding/marketing prose
Reference:
- [ ] `dydo/reference/dydo-commands.md` + template (content current; tone pass)
- [ ] `dydo/reference/guardrails.md` (content current; tone pass)
- [ ] `dydo/reference/configuration.md` (schema current; prose pass)
- [ ] `dydo/reference/roles/*.md` ×9 (role reference pages)
Understand:
- [ ] `dydo/understand/architecture.md` (+ `Templates/architecture.template.md` —
      note: the template is the PROJECT-scaffold version, separate content)
- [ ] `dydo/understand/guard-system.md` (staged-onboarding narrative largely removed;
      re-center on off-limits + nudges)
- [ ] `dydo/understand/task-lifecycle.md` (DR-036 lifecycle is current; strip
      assigned-agent framing)
- [ ] `dydo/understand/roles-and-permissions.md`, `documentation-model.md`,
      `templates-and-customization.md`, `about.md`, `_index.md`/`_understand.md` hubs
Guides:
- [ ] `dydo/guides/getting-started.md` — the onboarding walkthrough (claim-heavy)
- [ ] `dydo/guides/coding-standards.md` + `Templates/coding-standards.template.md`
- [ ] `dydo/guides/customizing-roles.md`, `testing-strategy.md`,
      `troubleshooting.md`, `orchestration-pitfalls.md` (both already trimmed;
      tone pass), `_guides.md`/`_index.md` hubs
- [ ] `Templates/_project.template.md` (tasks-folder note)

## 6. After the pass — mechanics

- [ ] `dydo sync` — recompile skills/agents from the reworked sources; eyeball one
      compiled SKILL.md end-to-end.
- [ ] `dydo template update` — refresh framework hashes for edited templates.
- [ ] `dydo check` — link/naming validation over the reworked tree.
- [ ] Keep the `about-dynadocs` .md/.template pair byte-identical (a test enforces it).
- [ ] Re-arm the guard (`notguard` → `guard`) once you're satisfied; verify hooks fire.
- [ ] Then: release ritual (version bump is yours).
