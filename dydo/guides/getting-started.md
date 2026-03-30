---
area: guides
type: guide
---

# Getting Started

First-time setup walkthrough: install dydo, initialize a project, and run your first agent session.

---

## Prerequisites

You need one of:

- **Node.js** (v18+) — for the npm package
- **.NET 10** — for the dotnet tool

And **Claude Code** — DynaDocs uses Claude Code's `PreToolUse` hook system for guard enforcement.

---

## Step 1: Install dydo

```bash
# npm (recommended)
npm install -g dydo

# or, if you have .NET
dotnet tool install -g dydo
```

Verify the installation:

```bash
dydo version
```

---

## Step 2: Set DYDO_HUMAN

Agents need to know who they belong to. Set the `DYDO_HUMAN` environment variable to your name:

```bash
# Bash/Zsh — add to your shell profile
export DYDO_HUMAN="your_name"

# PowerShell — add to your profile
$env:DYDO_HUMAN = "your_name"
```

Use a short, consistent identifier (e.g., first name, username). This ties agents to you and prevents cross-human conflicts in team setups.

---

## Step 3: Initialize your project

Run from your project's root directory:

```bash
dydo init claude
```

This creates:

- The `dydo/` folder structure with documentation scaffolding
- Agent workspaces and workflow files
- Guard hooks (Claude Code only — wired automatically)
- A default set of agents assigned to you

**Options:**

```bash
dydo init claude --name "alice" --agents 3   # Non-interactive setup
```

---

## Step 4: Link your AI entry point

Add this to your `CLAUDE.md`:

```markdown
This project uses an agent orchestration framework (dydo).
Before starting any task, read [dydo/index.md](dydo/index.md) and follow the onboarding process.
```

This is how the AI discovers dydo on each session.

---

## Step 5: Verify the setup

```bash
dydo check          # Validate documentation structure
dydo agent list     # See your available agents
```

`dydo check` reports any issues with frontmatter, links, or naming. Fix them with:

```bash
dydo fix            # Auto-fix what's possible
```

---

## Your first agent session

Start a session by naming an agent in your prompt:

```
Hey Adele, help me fix this bug in the auth service
```

What happens:

1. The AI reads `CLAUDE.md`, gets redirected to `dydo/index.md`
2. It navigates to `dydo/agents/Adele/workflow.md`
3. Claims its identity: `dydo agent claim Adele`
4. Reads the prompt, infers the appropriate role, and sets it
5. The guard hook enforces role-based permissions on every file operation

The agent onboards itself — no manual context-setting needed. The only workflow flag is `--inbox`, used when agents are dispatched by other agents to check their inbox for work items.

---

## Joining an existing project

If dydo is already set up and you're joining the team:

```bash
dydo init claude --join
```

This assigns you a pool of agents without overwriting the existing `dydo/` structure.

---

## Configuring Claude Code

The `CLAUDE.md` pointer is the bridge between Claude Code and dydo. It tells the AI to read `dydo/index.md` at session start, which kicks off the onboarding process.

**Tip:** [Obsidian](https://obsidian.md) makes navigating the documentation easier. If Obsidian converts links when you move files, run `dydo fix` afterward.

---

## Next steps

- Fill in `dydo/understand/about.md` with your project context
- Customize `dydo/guides/coding-standards.md` to match your conventions
- Edit templates in `dydo/_system/templates/` to fit your workflow

---

## Related

- [About DynaDocs](../reference/about-dynadocs.md) — What dydo is and how it works
- [Configuration Reference](../reference/configuration.md) — dydo.json and role files
- [Troubleshooting](./troubleshooting.md) — Common errors and recovery
