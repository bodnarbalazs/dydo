---
area: general
type: reference
---

# Glossary

Definitions of domain-specific terms used throughout this project.

---

## How to Use

- Link to specific terms: `[Term](./glossary.md#term-name)`
- Terms are alphabetized
- Each term has an anchor matching its kebab-case name

---

## DynaDocs Terms

### Agent

A named identity that an AI assistant operates under during a work session. Agents have unique names (Adele, Brian, Charlie, etc.) and dedicated workspaces. Each agent can have a role that determines file permissions.

**See also:** [Workflow](./workflow.md)

### Claim

The action of registering as a specific agent for a terminal session. Run `dydo agent claim <name>` to claim an agent. An agent can only be claimed if it's assigned to the current human (via `DYDO_HUMAN`).

### Dispatch

Sending a task or request to another agent. Used for handoffs between phases (e.g., code-writer → reviewer). Run `dydo dispatch --role <role> --task <task> --brief "..."`.

### DYDO_HUMAN

Environment variable that identifies which human is operating the terminal. Set via `export DYDO_HUMAN=yourname`. This determines which agents are claimable from `dydo.json` assignments.

### Guard

The permission enforcement system. The `dydo guard` command is called by hooks (e.g., Claude Code PreToolUse) to check if the current agent's role allows editing a file. Returns exit code 0 (allow) or 2 (block).

### Human

A person who operates the terminal. Identified by `DYDO_HUMAN` environment variable. Humans are assigned agents in `dydo.json` and can only claim agents assigned to them.

### Role

A permission level that determines what files an agent can edit. Available roles: `code-writer`, `reviewer`, `docs-writer`, `interviewer`, `planner`. Set via `dydo agent role <role>`.

### Workspace

An agent's dedicated folder at `dydo/agents/{AgentName}/`. Contains `state.md` (current state), `.session` (session info), and `inbox/` (dispatched messages). Workspaces are gitignored.

---

## Project Terms

### Example Term

Brief definition (1-2 sentences). Include context about when/where this concept applies.

**See also:** [Related Doc](./understand/related.md)

---

<!--
Add terms alphabetically. Format:

### Term Name

Definition. Context.

**See also:** [Link](./path.md) (optional)

-->
