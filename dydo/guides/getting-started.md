---
area: guides
type: guide
---

# Getting Started

First-time setup walkthrough: install dydo, initialize a project, compile the skills, and run your first work session.

---

## Prerequisites

- A git repository (dydo's records and docs live in it; git is the safety net)
- Claude Code or Codex as your coding agent runtime

## Step 1: Install dydo

```bash
# npm (recommended)
npm install -g dydo

# or, if you have .NET
dotnet tool install -g dydo
```

## Step 2: Initialize your project

Run from your project's root:

```bash
dydo init claude    # or: dydo init codex
```

This creates the `dydo/` knowledge tree, the templates, the guard hooks for your runtime, and the entry-point file (`CLAUDE.md` for Claude Code, `AGENTS.md` for Codex) that points every session at [dydo/index.md](../index.md).

## Step 3: Compile the skills

```bash
dydo sync
```

The mode templates compile into your platform's skills and agents (planner, code-writer, reviewer with its per-target resources, …), plus the shipped workflows (`run-sprint`, `inquisition`). Re-run after any template change.

## Step 4: Fill in your context

The docs are the source of truth agents work from — the more real they are, the better the work:

- `dydo/understand/about.md` — what this project is
- `dydo/understand/architecture.md` — how it's built
- `dydo/guides/coding-standards.md` — your conventions

Then validate:

```bash
dydo check          # Report issues (frontmatter, links, naming)
dydo fix            # Auto-fix what's possible
```

## Step 5: Your first work session

Open your coding agent in the repo and just talk. The entry file routes it: docs first, skills for the work.

The full loop for a real feature:

1. **Think it through** — the co-thinker skill hashes out the design with you; conclusions land as decision records.
2. **Plan it** — the planner skill turns the ripe design into a sprint root + slice files.
3. **Gate it** — a fresh-eyes reviewer (plan resource) passes the plan; the sprint flips `active`.
4. **Run it** — the orchestrator skill drives `run-sprint`: each slice implemented and reviewed, merged serially, audited as a whole.

For a trivial edit — a typo, a one-liner — skip the machinery: if it needs a reviewer, it needs a plan.

## Joining an existing project

```bash
dydo init claude --join
```

Wires up this machine's hooks and entry files without touching the existing `dydo/` tree.

---

**Tip:** [Obsidian](https://obsidian.md) makes navigating the docs easier. If Obsidian converts links when you move files, run `dydo fix` afterward.

## Related

- [About DynaDocs](../reference/about-dynadocs.md) — What dydo is and how it works
- [Customizing Roles](./customizing-roles.md) — Make the skills yours
- [Configuration Reference](../reference/configuration.md) — dydo.json
