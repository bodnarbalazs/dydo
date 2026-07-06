# DynaDocs (dydo)

Own your project's knowledge — then put agents to work on it.

DynaDocs (dydo) is the **policy and context layer** for AI coding agents. It gives Claude Code's native agents structured, versioned project knowledge to work from, and a guard that enforces your rules of the road on every action they take. **Claude owns the engine — spawning, scheduling, isolation, fan-out. dydo owns the map and the rules.**

Your knowledge lives in your repo as human-readable, git-diffable docs — the single source of truth. Turn on the optional Notion sync and your team gets a live PM board as a *view*; the data never leaves your control.

**Built for Claude Code.** dydo uses Claude Code's `PreToolUse` hook for guard enforcement and compiles into its native subagents, skills, and workflows. Support for other AI coding tools may come later.

> 📖 **Full docs, diagrams, and command reference:** [github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)

## Stop Doing Agent Work Yourself

Your time is the most precious resource in the equation. Focus on your comparative advantage: deciding **what** should be done and **why**. Everything that *can* be done by an agent *should* be. Agents write code, review code, write tests, write docs, and coordinate other agents. The human is the last step, not the first reviewer.

dydo makes this dependable. On its own, an AI agent starts every session cold and works without rails. dydo gives agents **persistent, structured memory** (your docs), **enforced guardrails** (a guard hook that fires on every action — including inside subagents and workflows), and a **compiler** that turns your roles and docs into the native agents and skills Claude Code runs.

### What you get

- **Documentation as memory** — Your docs are the source of truth; agents re-read them each session.
- **Universal guardrails** — Off-limits paths (hard block) and custom nudges (regex → block or warn) enforced on *every* tool call, in the main thread and inside every subagent and workflow.
- **Compiles to native Claude Code** — `dydo sync` turns your roles and docs into `.claude/agents/` and `.claude/skills/`.
- **No self-review** — The agent that wrote the code doesn't review it.
- **Native orchestration** — Flagship workflows: `run-sprint` (sliced code→review→merge→audit) and `inquisition` (multi-lens QA gate).
- **Model tiers** — Roles declare `strong` / `standard` / `light`; the compiler binds the concrete model.
- **Notion as a view (optional)** — Two-way sync to a team PM board. Repo files stay canonical; you own the data.

## What Changed in 2.0.0

Almost everything. Earlier versions shipped dydo's *own* multi-agent runtime — dispatching workers into terminal tabs with inbox, messaging, queues, and worktree plumbing. Then Claude Code introduced dynamic workflows and governable subagents, and dydo's hand-rolled orchestration was suddenly *fighting* the native runtime instead of riding it.

So 2.0 pivots: **Claude Code owns orchestration** (subagents, skills, workflows), and **dydo becomes the policy + context layer** that plugs into it. Worker dispatch, per-role RBAC, the audit trail, and the inquisitor/judge roles are gone; `dydo sync`, two-tier identity, model tiers, and optional Notion sync are new; the guard and documentation system are kept and sharpened.

## Installation

```bash
# npm (recommended)
npm install -g dydo

# if you have .NET installed (faster install)
dotnet tool install -g dydo
```

Set the `DYDO_HUMAN` environment variable so agents know who they belong to:

```bash
# macOS / Linux (add to ~/.bashrc or ~/.zshrc)
export DYDO_HUMAN="YourName"
```

```powershell
# Windows (PowerShell)
[Environment]::SetEnvironmentVariable("DYDO_HUMAN", "YourName", "User")
```

## Quick Start

```bash
dydo init claude    # scaffold the dydo/ tree + install the guard hook
dydo sync           # compile roles → .claude/agents + skills
dydo check          # validate documentation
```

Then add this to your `CLAUDE.md`:

```markdown
This project uses an agent orchestration framework (dydo).
Before starting any task, read [dydo/index.md](dydo/index.md) and follow the onboarding process.
```

Fill out `about.md` with your project context, adjust `coding-standards.md`, and set your source/test paths in `dydo.json`. See the [full guide on GitHub](https://github.com/bodnarbalazs/dydo) for roles, nudges, model tiers, and Notion sync.

## Agent Roles

dydo ships **seven base roles** — `chief-of-staff`, `co-thinker`, `orchestrator` (Tier-1 managers you talk to), and `code-writer`, `reviewer`, `test-writer`, `docs-writer` (workers `dydo sync` compiles into native subagents). Plus non-claimable skills/agents: `planner`, `sprint-auditor`, and `inquisitor`. Roles are data-driven `.role.json` files; add your own with `dydo roles create <name>`.

## License

AGPL-3.0 — [github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)

**Free to use, always.** You can use dydo as a tool on any project, including commercial ones. The AGPL obligations apply only if you modify or embed dydo's source code in your own software — for example, shipping dydo as part of a product you distribute or offer as a service.

For commercial licensing without AGPL obligations, [open a GitHub issue](https://github.com/bodnarbalazs/dydo/issues).
