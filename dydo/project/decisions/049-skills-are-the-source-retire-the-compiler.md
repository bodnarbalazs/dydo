---
area: project
type: decision
status: accepted
date: 2026-09-12
participants: [balazs, opencode admiral, Astra]
---

# 049 — Skills Are the Source: Retire the Compiler

dydo stops compiling skills and agents from its own templates. **A skill folder is the source of
truth**, authored directly in the cross-vendor `SKILL.md` format; distribution is the hosts' job.
The compiler (`dydo sync`), `template update`, the `dydo.json.skills` switchboard, include tags and
their re-anchoring, framework hashes and managed-output cleanup are retired. Any host-specific agent
configuration that remains is small, explicit and hand-maintained per host actually used — never
generated. The guard and nudges, the documentation structure and `dydo check`, the testing-runner
proxy, and cheap scaffolding survive. This amends the "compiler is the crown jewel" clause of
[Decision 041](./041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md).

---

## Context

[Decision 024](./024-dydo-2-native-pivot.md) gave dydo a compiler; Decision 041 kept it as *the*
anti-lock-in layer: "author once, run on any vendor; nobody else has this." That was a deliberate,
defensible product bet. Four things have changed the premise:

1. **Skills became a shared standard.** Claude Code and Codex both follow the open
   [agentskills.io](https://agentskills.io) standard: a skill is `<name>/SKILL.md` with `name` and
   `description`, plus optional `references/`, `scripts/`, `assets/`. Claude reads `.claude/skills/`,
   Codex reads `.agents/skills/` (scanning up to the repo root). The *body* — the thing dydo spent
   its compiler on — is already portable.
2. **Both hosts support symlinked skill folders**, and both now ship real distribution
   (Claude plugins/marketplaces; Codex plugins and `$skill-installer`). One canonical folder,
   linked or copied into each host's discovery path, delivers author-once with **zero transform**.
3. **dydo is a personal workflow others may fork, not a product for others.** The README sketch says
   so outright ("Don't. Seriously. Build your own system."). The compiler's second machine —
   includes, re-anchoring, content hashes, the switchboard, managed cleanup — exists to protect
   *downstream consumers* who do not exist. For the author, git is the update mechanism.
4. **The empirical record.** In the 3.0 delivery, crews ran as plain general agents told to load a
   skill by path. No compiled `.claude/agents/*` type was on the critical path; the prior handoff
   records that the compiled `issue-captain` agent type lacked the tools the work needed. The
   compiler's *output* was not load-bearing for the workflow its *input* describes.

The cost is concrete: a translate-and-diff engine, a large test surface for it, and a new per-host
dialect every time a host is added (opencode being the immediate case). That maintenance competes
directly with work on real projects, which is the opposite of what dydo is for.

## Options Considered

### Option A: Keep the compiler, add opencode as a third target

Honours DR 041 as written. One authored role still yields each host's skill *and* agent config.

- **Pros:** preserves the anti-lock-in claim for both the body and the agent config; no migration.
- **Cons:** each new host is a new dialect with its own translation and tests; the body half is now
  redundant with the standard; the product machinery still serves no consumer. This is the treadmill.

### Option B: Narrow the compiler (keep a thin copy + agent translation; drop the product machinery)

- **Pros:** less surface; keeps one-source ergonomics.
- **Cons:** still an engine to own and test; still translates the fastest-moving part of each host
  (agent config); still reimplements what symlink-and-copy already does for the body.

### Option C: Skills are native folders; agent config minimal and hand-maintained (chosen)

- **Pros:** one artifact per role; a new host is a new folder, not a new compiler target; the test
  surface shrinks to behaviour actually retained; distribution is delegated to the hosts.
- **Cons:** gives up generated agent config and its hard guarantees; a few duplicated fields per
  host; installing/relocating skills becomes an explicit step that must be verified, not assumed.

## Decision

1. **The skill folder is the source.** `skills/<name>/SKILL.md` (with optional `references/`,
   `scripts/`, `assets/` per the standard) is authored directly. No prose is compiled. Resources and
   project links are written to resolve where they are installed.
2. **Distribution is the host standard.** One canonical folder is symlinked or copied to each host's
   discovery location (`.claude/skills`, `.agents/skills`, and whatever opencode reads). Host
   plugins/installers are an available option, never a requirement.
3. **The compiler is retired.** `dydo sync` as a compiler, `dydo template update`, the
   `dydo.json.skills` switchboard, `{{include:...}}` and re-anchoring, framework content hashes and
   managed-output cleanup are deleted. Their tests are deleted when their behaviour is retired —
   not kept dormant.
4. **Project customization is local editing.** A project edits its copy and references project
   documents. Deliberate divergence from the framework is accepted; there is **no promise of
   automatic reconciliation**.
5. **Agent configuration is ordinary, minimal, hand-maintained.** Roles are skills. Keep at most a
   small native agent definition per host actually used, and only where a hard guarantee is
   load-bearing — the candidate is a **read-only reviewer**. Do not generate it. Add a tiny
   generation script only if hand-maintenance becomes a demonstrated burden. Do not solve every
   host's configuration model in advance.
6. **What survives:** the documentation structure and `dydo check`/link checking; the guard and its
   dangerous-pattern detection; the nudge system (regex plus a helpful message); the testing-runner
   (`gap_check`) proxy; and scaffolding (`dydo init`) while it stays cheap and reliable — a sample
   tree plus a copy operation is its sufficient eventual implementation.
7. **Stopping rule.** Change dydo when a real project exposes recurring friction; require
   demonstrated need before adding another general capability. A test count proves neither value nor
   overengineering.

## Consequences

- **Gained:** a smaller surface, host-native artifacts, a cheap new host, fewer tests, and attention
  returned to real projects.
- **Accepted:** generated agent config and its hard guarantees are gone; a few config fields are
  duplicated per host; installing/relocating skills is a manual step; **installation must be
  acceptance-checked** (see below); the 3.0 release's remaining generator/template work must be
  re-scoped.
- **Installation acceptance check** (so "tell an agent to install it" does not become recurring
  troubleshooting): the host's skill list shows the role; a spawned agent told only *"load the
  `<role>` skill"* loads the body **and a resource-only detail**; the skill's links resolve from the
  installed location on each host; explicit-only invocation behaves where required.
- **Open, left to the pilot:** whether the read-only reviewer needs a native agent. Start pure-skill;
  add the native file only when a real run shows a reviewer writing what it should not.
- **Migration is incremental, not a rewrite.** Pause compiler expansion, including opencode. Carry
  **one role and its resources** to both hosts, run it on real work, and verify the acceptance
  check. Then migrate role by role, deleting the machinery each vacates. Keep every working utility
  until its replacement is proven.
- **The same scrutiny applies to the workflow ceremony.** Captains, review stages, merge Issues and
  mandatory records must each pay for their recurring cost. Retiring the compiler will not help
  enough if a small change still demands excessive ceremony. That audit is its own next step.

---

## Affects

- [Decision 041](./041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md) — its compiler clause is amended by this record
- [Architecture Overview](../../understand/architecture.md)
- [Templates and Customization](../../understand/templates-and-customization.md)
- [Customizing Roles](../../guides/customizing-roles.md)
- [Guard System](../../understand/guard-system.md) — retained, unchanged
- [dydo Commands Reference](../../reference/dydo-commands.md)
- [About This Project](../../understand/about.md)
- [Decision 024](./024-dydo-2-native-pivot.md) — the pivot this completes
