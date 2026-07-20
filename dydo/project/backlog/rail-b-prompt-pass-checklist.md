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

- [ ] **`Templates/entry-point.template.md`** ← THIS generates both CLAUDE.md and
      AGENTS.md (one runtime-neutral template, `{{PROJECT_NAME}}` placeholder). Just
      promoted from a 4-line inline C# string so you can author it as markdown — make
      it as verbose as you want; `dydo init` materializes it (WriteIfNotExists —
      existing projects keep their copy).
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

> **2026-07-17 progress (DR-042 wave, with Fable):** plan-first doctrine landed —
> [DR-042](../decisions/042-plan-first-implementation.md) (spec+plan format, prohibition);
> coding-standards §5 rebuilt in both template and live guide; `mode-planner` rewritten to the
> root+slices format; `mode-reviewer` de-ceremonied + Review Targets section; the plan rubric
> ships as `Templates/skill-references/reviewer/plan.md` and `dydo sync` now emits skill
> `references/` folders (both Claude and Codex paths). Remaining mode templates still need
> your pass.

## 3. Compiler sources — the actual prompt engineering

- [ ] `Templates/mode-*.template.md` — code-writer/planner/reviewer are DONE (2026-07-17:
      de-ceremonied, no placeholders, role-specific workflow in the role file). The other
      six (chief-of-staff, co-thinker, docs-writer, orchestrator, sprint-auditor,
      test-writer) still carry claim-era prose + `{{AGENT_NAME}}`.
      **Parked for the manager templates** (orchestrator/chief-of-staff), all blessed
      2026-07-17: the trivial-edit exception ("if it needs a reviewer, it needs a plan" —
      the spawn-time call); the delegation rule (discovery subagents freely; implementation
      only through worker skills in a reviewed workflow); commit discipline (workers never
      commit; the orchestrator commits a slice exactly when its review passes — one slice,
      one commit; uncommitted ⇒ un-reviewed); the worktree lifecycle (orchestrator assigns
      parallel lanes their worktrees per the plan's Ordering & isolation section, merges
      passed slices back serially; the audit verifies the merged seam).
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
- [x] `dydo/glossary.md` — defines claim/dispatch/inbox-era terms
- [ ] `dydo/reference/about-dynadocs.md` + `Templates/about-dynadocs.template.md`
      (byte-identical pair!) + `README.md` — onboarding/marketing prose
Reference:
- [x] `dydo/reference/dydo-commands.md` + template — reconciled byte-equal; dup Model
      Commands cut, empty Role Commands + Role Permissions tail removed, stale
      option/desc lines fixed (docs-hygiene pass, 2026-07-20)
- [x] `dydo/reference/guardrails.md` — DELETED; surviving catalog folded into
      `understand/guard-system.md`
- [x] `dydo/reference/configuration.md` — Custom Roles section now template-is-the-role
- [x] `dydo/reference/roles/*.md` — DELETED (generated claim-era pages; the mode
      template is the role)
Understand:
- [x] `dydo/understand/architecture.md` (+ `Templates/architecture.template.md` —
      note: the template is the PROJECT-scaffold version, separate content)
- [x] `dydo/understand/guard-system.md` (staged-onboarding narrative largely removed;
      re-center on off-limits + nudges)
- [x] `dydo/understand/task-lifecycle.md` (DR-036 lifecycle is current; strip
      assigned-agent framing)
- [x] `dydo/understand/roles-and-permissions.md` (DELETED),, `documentation-model.md`,
      `templates-and-customization.md`, `about.md`, `_index.md`/`_understand.md` hubs
Guides:
- [x] `dydo/guides/getting-started.md` — the onboarding walkthrough (claim-heavy)
- [x] `dydo/guides/coding-standards.md` + `Templates/coding-standards.template.md`
- [x] `dydo/guides/customizing-roles.md`, `testing-strategy.md`,
      `troubleshooting.md`, `orchestration-pitfalls.md` (both already trimmed;
      tone pass), `_guides.md`/`_index.md` hubs
- [ ] `Templates/_project.template.md` (tasks-folder note)

## 6. After the pass — mechanics

- [ ] `dydo sync` — recompile skills/agents from the reworked sources; eyeball one
      compiled SKILL.md end-to-end.
- [ ] `dydo template update` — refresh framework hashes for edited templates.
- [x] `dydo check` — 0 errors, 0 warnings (was 38/52; extractor false-positives fixed
      in code, resolves issue 0287)
- [ ] Keep the `about-dynadocs` .md/.template pair byte-identical (a test enforces it).
- [ ] Re-arm the guard (`notguard` → `guard`) once you're satisfied; verify hooks fire.
- [ ] Then: release ritual (version bump is yours).
