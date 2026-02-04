# DynaDocs (dydo) - Complete Specification v3

## Background & Context

### The Project
DynaDocs is a platform-agnostic documentation and AI agent orchestration system. It can be used with any project but is designed to support:
- Dynamic documentation that AI agents can traverse efficiently
- Multi-agent workflows with role-based permissions
- Multi-user collaboration with agent assignments

### The Problem We're Solving

AI agents work best with:
1. **Predictable structure** - AI needs to know where things are
2. **Relative links** - AI can directly resolve paths without searching
3. **Consistent naming** - No spaces or weird casing that breaks path resolution
4. **Standard format** - Frontmatter and summaries help AI understand docs without reading everything
5. **Hierarchical navigation** - Start broad, drill down to specifics
6. **Graph connectivity** - Related docs link to each other bidirectionally
7. **Agent coordination** - Multiple AI agents working on different parts of a project need isolation and handoff mechanisms

**The specific pain points:**
- Obsidian converts relative links to `[[wikilinks]]` when files are moved
- Spaces in filenames cause path parsing issues
- No enforced structure means inconsistent docs
- No way to validate the docs are AI-traversable
- Multiple humans using AI agents need isolated agent pools

---

## Tool Specification

### Name
- **Full name**: DynaDocs
- **CLI command**: `dydo`
- **Meaning**: Dynamic Documentation (Dy = Dynamic docs, Do = Agent orchestration)

### Exit Codes
- `0` - Success, no issues (also: action allowed for guard)
- `1` - Validation errors found
- `2` - Tool error / action blocked (for guard hooks)

---

## Folder Structure

### Overview

DynaDocs uses a unified `dydo/` folder that contains both documentation ("Dy") and agent orchestration ("Do"):

```
project/
├── dydo.json                    # Config at root (committed)
├── CLAUDE.md                    # Entry point → dydo/index.md (committed)
│
└── dydo/                        # Main DynaDocs folder
    ├── index.md                 # Entry point (committed)
    ├── glossary.md              # Term definitions (committed)
    │
    │── _system/                 # System configuration (committed)
    │   └── templates/           # Project-local template overrides
    │       ├── agent-workflow.template.md
    │       └── mode-*.template.md
    │
    │── understand/              # ┐
    │── guides/                  # │ The "Dy" - Documentation
    │   └── api/                 # │ Each subfolder has:
    │       ├── _api.md          # │   - _foldername.md (meta file)
    │       └── _index.md        # │   - _index.md (hub file)
    │── reference/               # │ (committed to git)
    │── project/                 # │
    │   ├── tasks/               # │ ← Cross-human dispatch
    │   ├── decisions/           # │
    │   ├── pitfalls/            # │
    │   └── changelog/           # ┘
    │
    └── agents/                  # The "Do" - Orchestration (GITIGNORED)
        ├── Adele/
        │   ├── workflow.md      # Agent-specific workflow (generated from template)
        │   ├── state.md         # Role, task, permissions, role history
        │   ├── .session         # PID tracking
        │   ├── inbox/           # Messages from other agents
        │   └── modes/           # Role-specific guidance files
        │       ├── code-writer.md
        │       ├── reviewer.md
        │       ├── tester.md
        │       └── ... (all 7 modes)
        ├── Brian/
        └── ... (all agents)
```

### What Gets Committed vs Gitignored

| Path | Committed | Why |
|------|-----------|-----|
| `dydo.json` | Yes | Team configuration |
| `CLAUDE.md` | Yes | Entry point for AI agents |
| `dydo/_system/templates/` | Yes | Team-customized templates |
| `dydo/` (except agents/) | Yes | Documentation is shared truth |
| `dydo/project/tasks/` | Yes | Cross-human task handoff |
| `dydo/agents/` | **No** | Local per-machine state (includes generated workflow/mode files) |

**.gitignore entry:**
```
dydo/agents/
```

### Documentation Folder Purposes

| Folder | Question it answers | Content type |
|--------|---------------------|--------------|
| `_system/templates/` | "How should generated files look?" | Customizable templates for agent files |
| `understand/` | "What IS this?" | Domain concepts, business logic, architecture |
| `guides/` | "How do I DO this?" | Step-by-step task instructions |
| `reference/` | "What are the specs?" | API docs, config options, tool docs |
| `project/` | "Why/how do we work?" | Decisions, pitfalls, changelog, tasks |
| `*/_foldername.md` | "What is this folder for?" | Purpose description for the folder |

---

## Configuration (dydo.json)

The `dydo.json` file at project root configures DynaDocs:

```json
{
  "version": 1,
  "structure": {
    "root": "dydo",
    "tasks": "project/tasks"
  },
  "agents": {
    "pool": [
      "Adele", "Brian", "Charlie", "Dexter", "Emma", "Frank",
      "Grace", "Henry", "Iris", "Jack", "Kate", "Leo",
      "Mia", "Noah", "Olivia", "Paul", "Quinn", "Rose",
      "Sam", "Tara", "Uma", "Victor", "Wendy", "Xavier",
      "Yara", "Zack"
    ],
    "assignments": {
      "balazs": ["Adele", "Brian", "Charlie", "Dexter", "Emma"],
      "alice": ["Frank", "Grace", "Henry", "Iris", "Jack"]
    }
  },
  "integrations": {
    "claude": true
  }
}
```

### Configuration Fields

| Field | Required | Description |
|-------|----------|-------------|
| `version` | Yes | Config schema version (currently 1) |
| `structure.root` | No | Root folder name (default: "dydo") |
| `structure.tasks` | No | Tasks folder path (default: "project/tasks") |
| `agents.pool` | Yes | List of agent names in use |
| `agents.assignments` | Yes | Map of human → assigned agents |
| `integrations` | No | Which integrations are enabled |

---

## Human Identity (DYDO_HUMAN)

Each human user must set the `DYDO_HUMAN` environment variable on their machine:

```bash
# Bash/Zsh (add to ~/.bashrc or ~/.zshrc)
export DYDO_HUMAN=balazs

# PowerShell (add to $PROFILE)
$env:DYDO_HUMAN = "balazs"

# Windows (permanent)
setx DYDO_HUMAN balazs
```

This enables:
- **Agent assignment validation**: Only claim agents assigned to you
- **Cross-human dispatch**: Route work to the right human's agents
- **Audit trail**: Know which human initiated each action

---

## Preset Agent Names

Two sets of preset names are available:

**Set 1 (Primary - 26 agents):**
Adele, Brian, Charlie, Dexter, Emma, Frank, Grace, Henry, Iris, Jack, Kate, Leo, Mia, Noah, Olivia, Paul, Quinn, Rose, Sam, Tara, Uma, Victor, Wendy, Xavier, Yara, Zack

**Set 2 (Overflow - 26 more):**
Alfred, Bella, Carla, Dylan, Ethan, Fiona, George, Holly, Ivan, Julia, Kevin, Luna, Marcus, Nadia, Oscar, Penny, Quentin, Rita, Steve, Tina, Ulrich, Vera, Walter, Xena, Yuri, Zara

If a project needs more than 26 agents, it draws from Set 2. Users can also customize names in `dydo.json`.

---

## Commands

### Documentation Commands

```bash
dydo check              # Validate all docs, report violations
dydo check <path>       # Check specific file or directory
dydo fix                # Auto-fix issues that can be fixed automatically
dydo fix <path>         # Fix specific file or directory
dydo index              # Regenerate Index.md from doc structure
dydo graph <file>       # Show graph connections for a file
dydo graph <file> --incoming        # Show backlinks
dydo graph <file> --degree <n>      # Show docs within n hops
dydo graph stats                    # Show top 100 docs by incoming links
dydo graph stats --top <n>          # Show top n docs by incoming links
```

### Agent Lifecycle Commands

```bash
dydo whoami                          # Show current identity
dydo agent claim <name|letter>       # Claim specific agent
dydo agent claim auto                # Claim first free agent for current human
dydo agent release                   # Release current agent
dydo agent status [name]             # Show agent status
dydo agent list                      # List all agents
dydo agent list --free               # List free agents
dydo agent role <role> [--task X]    # Set role and permissions
```

### Agent Management Commands

```bash
dydo agent new <name> <human>        # Create new agent, assign to human
dydo agent rename <old> <new>        # Rename an agent
dydo agent remove <name> [--force]   # Remove agent from pool
dydo agent reassign <name> <human>   # Reassign agent to different human
```

### Workflow Commands

```bash
dydo dispatch --role <role> --task <name> --brief "..." [--to <human>]
dydo inbox list                      # Agents with pending items
dydo inbox show                      # Show current agent's inbox
dydo inbox clear                     # Clear processed items
dydo review complete <task> --status pass|fail [--notes "..."]
```

### Task Commands

```bash
dydo task create <name> [--description "..."]
dydo task ready-for-review <name> --summary "..."
dydo task approve <name>             # Human only
dydo task reject <name> --notes "..." # Human only
dydo task list [--needs-review]
```

### Setup & Maintenance Commands

```bash
dydo init <integration>              # Initialize (claude, none)
dydo init <integration> --join       # Join existing project
dydo guard                           # Check permissions (for hooks)
dydo clean <agent>                   # Clean agent workspace
dydo clean --all                     # Clean all
dydo workspace init                  # Initialize agent workspaces
dydo workspace check                 # Verify workflow before session end
```

---

## Guard Command (Hook Integration)

The `dydo guard` command is designed to work as a hook for AI coding assistants. It's **platform-agnostic** but currently supports Claude Code.

### Input Modes

**Mode 1: Stdin JSON (Hook Mode)**
When called by a hook, receives JSON via stdin:
```json
{
  "tool_name": "Edit",
  "tool_input": {
    "file_path": "/absolute/path/to/file.cs"
  }
}
```

**Mode 2: CLI Arguments (Manual Testing)**
```bash
dydo guard --action edit --path src/Auth.cs
```

The command auto-detects which mode based on whether stdin has data.

### Output Format

**Allow action:**
- Exit code: `0`
- Stdout: (silent - no output)

*Why silent?* The guard runs on every file operation. Any output would accumulate and rot the agent's context over a session. Agents needing orientation should use `dydo whoami`.

**Block action:**
- Exit code: `2`
- Stderr: Error message explaining why blocked

Example stderr messages:

**Write blocked (no identity):**
```
BLOCKED: No agent identity assigned to this process.
  Run 'dydo agent claim auto' to claim an agent identity.
```

**Write blocked (no role):**
```
BLOCKED: Agent Adele has no role set.
  Run 'dydo agent role <role>' to set your role.
```

**Read blocked (no identity):**
```
BLOCKED: Read access denied.
  No agent identity assigned to this process.
  Read your workflow.md to learn how to onboard:
    dydo/agents/*/workflow.md
  Then run: dydo agent claim auto
```

**Read blocked (no role):**
```
BLOCKED: Read access denied.
  Agent Adele has no role set.
  Read your mode files to understand available roles:
    dydo/agents/Adele/modes/*.md
  Then run: dydo agent role <role>
```

**Role violation:**
```
BLOCKED: Agent Adele (reviewer) cannot edit src/Auth.cs. Reviewer role has no write permissions.
```

### Claude Code Hook Configuration

When `dydo init claude` runs, it creates/updates `.claude/settings.local.json`:

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write|Read|Bash",
        "hooks": [
          {
            "type": "command",
            "command": "dydo guard"
          }
        ]
      }
    ]
  }
}
```

### Staged Read Access Control

The guard enforces a **staged access control** system that progressively unlocks file access as agents complete onboarding steps. This ensures agents follow the proper workflow.

| Stage | Condition | Can Read | Can Write |
|-------|-----------|----------|-----------|
| 0 | No identity | Bootstrap files only | Nothing |
| 1 | Identity claimed | + own mode files | Nothing |
| 2 | Identity + role | Everything (except off-limits) | Per RBAC |

**Stage 0 - Bootstrap Files (no identity required):**
- Root-level files (`CLAUDE.md`, `.Cursorrules`, etc.)
- `dydo/index.md`
- `dydo/agents/*/workflow.md`

These files teach the agent how to claim an identity.

**Stage 1 - Mode Files (identity claimed, no role):**
- All Stage 0 files
- `dydo/agents/{self}/modes/*.md`

Mode files explain the available roles and help the agent choose one.

**Stage 2 - Full Access (identity + role):**
- All reads allowed (except off-limits patterns)
- Writes governed by role-based permissions (RBAC)

This staged approach ensures agents must:
1. Read workflow.md to learn the onboarding process
2. Claim an identity with `dydo agent claim`
3. Read mode files to understand available roles
4. Set a role with `dydo agent role`
5. Only then can they access project files

---

## Agent Roles and Permissions

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
| `reviewer` | `dydo/agents/{self}/**` | `**` (except own workspace) |
| `co-thinker` | `dydo/agents/{self}/**`, `dydo/project/decisions/**` | `src/**`, `tests/**` |
| `docs-writer` | `dydo/**` (except agents/) | `src/**`, `tests/**` |
| `interviewer` | `dydo/agents/{self}/**` | Everything else |
| `planner` | `dydo/agents/{self}/**`, `dydo/project/tasks/**` | `src/**` |
| `tester` | `dydo/agents/{self}/**`, `tests/**`, `dydo/project/pitfalls/**` | `src/**` |

### Role Descriptions

**Co-thinker vs Interviewer:**
- **Interviewer**: Start of workflow, produces formal requirements brief, typically a dispatch target
- **Co-thinker**: Any point in workflow, exploratory thinking with the human, can write decisions, typically a mode switch (context preserved)

**Tester:**
- Tests the application and reports issues
- Can write test files and create bug reports in `dydo/project/pitfalls/`
- Cannot modify source code directly
- Focus: finding bugs, edge cases, and usability issues

### Self-Review Prevention

The system enforces **no self-review** through task role history tracking:

- When an agent sets a role on a task, it's recorded in the state file
- An agent who was `code-writer` on a task **cannot** become `reviewer` on the same task
- This ensures "fresh eyes" validation - different agents review code

**Example blocked action:**
```
ERROR: Agent Adele cannot take role 'reviewer' on task 'jwt-auth'.
Reason: Adele was previously 'code-writer' on this task.
Self-review is not permitted. Dispatch to a different agent for review.
```

The role history is stored per-agent per-task and persists across sessions.

### Mode Files

Each agent workspace contains a `modes/` folder with role-specific guidance files:

```
dydo/agents/Adele/modes/
├── code-writer.md
├── reviewer.md
├── co-thinker.md
├── docs-writer.md
├── interviewer.md
├── planner.md
└── tester.md
```

When an agent sets a role via `dydo agent role <role>`, they should read their corresponding mode file for:
- Role-specific must-reads
- What they can and cannot do
- Workflow guidance for that role
- Links to relevant process documentation

Mode files are generated during `dydo init` and `dydo workspace init`, personalized with the agent's name.

### Mode Flags

Users can specify a mode when launching an agent session using flags in the prompt:

| Flag | Mode | Workflow |
|------|------|----------|
| `--feature` | Full feature workflow | Interview → Plan → Code → Review |
| `--task` | Standard task | Plan → Code → Review |
| `--quick` | Light implementation | Code only (for simple changes) |
| `--think` | Collaborative exploration | Co-thinker mode |
| `--review` | Code review | Reviewer mode |
| `--docs` | Documentation | Docs-writer mode |
| `--test` | Testing & validation | Tester mode |
| `--inbox` | Dispatched work | Check inbox for instructions |

**No flag?** The agent should infer the appropriate mode from the prompt intent. Ambiguous requests should prompt clarification.

**Examples:**
```
claude "Add user authentication --feature"    # Full workflow
claude "Fix the null check bug --quick"       # Just implement
claude "Help me think through caching --think" # Co-thinker
```

### Template Customization

Templates for agent workflows and mode files are copied to `dydo/_system/templates/` during `dydo init`. Teams can customize these templates, and changes are committed to git. When new agent workspaces are created, the system uses project-local templates if they exist, otherwise falls back to built-in defaults.

**How it works:**

1. `dydo init` copies all templates to `_system/templates/`
2. Teams customize the templates to fit their project
3. When `dydo agent new` creates an agent, project-local templates are used
4. Partial overrides work: delete files you don't want to customize

**Available templates:**

| Template | Purpose | Placeholders |
|----------|---------|--------------|
| `agent-workflow.template.md` | Agent entry point and onboarding | `{{AGENT_NAME}}` |
| `mode-*.template.md` | Role-specific guidance (7 files) | `{{AGENT_NAME}}` |

**Template placeholders:**

- `{{AGENT_NAME}}` - Agent name (e.g., "Adele")

**Example customization:**

```markdown
<!-- dydo/_system/templates/agent-workflow.template.md -->
---
agent: {{AGENT_NAME}}
type: workflow
---

# Welcome, {{AGENT_NAME}}!

You are working on the **Acme Corp** project.
Remember our core values: quality over speed, test everything.

## First Steps

1. Claim your identity: `dydo agent claim {{AGENT_NAME}}`
2. Read our coding standards
...
```

**Important:** `dydo init --join` does NOT overwrite existing templates (preserves team customizations).

---

## Command Output Language

All command outputs use **objective language** suitable for both human and AI readers.

### Examples

**dydo agent claim auto**
```
Agent identity assigned to this process: Adele
  Assigned human: balazs
  Workspace: dydo/agents/Adele/
```

**dydo agent claim Frank** (wrong human)
```
ERROR: Agent Frank is assigned to human 'alice', not 'balazs'.
Claimable agents for human 'balazs': Adele, Brian, Charlie, Dexter, Emma
Use 'dydo agent claim auto' to claim the first available.
```

**dydo whoami** (agent claimed)
```
Agent identity for this process: Adele
  Assigned human: balazs
  Role: code-writer
  Task: jwt-auth
  Status: working
  Workspace: dydo/agents/Adele/
```

**dydo whoami** (no agent claimed)
```
No agent identity assigned to this process.
  Human (from DYDO_HUMAN): balazs
  Claimable agents for balazs: Adele, Brian, Charlie, Dexter, Emma

To claim an agent, run:
  dydo agent claim auto       # Claims first available
  dydo agent claim Adele      # Claims specific agent
```

**dydo agent list**
```
Agent       Status    Role           Task                 Human
────────────────────────────────────────────────────────────────
Adele       working   code-writer    jwt-auth             balazs
Brian       free      -              -                    balazs
Charlie     free      -              -                    balazs
Frank       working   reviewer       jwt-auth-review      alice
Grace       free      -              -                    alice
...

5 free, 2 working
```

**dydo guard** (blocked - stderr)
```
Agent Adele (reviewer) cannot edit src/Auth.cs. Reviewer role has no write permissions.
```

---

## Init Command

### Syntax

```bash
dydo init <integration>              # Fresh setup
dydo init <integration> --join       # Join existing project
```

Where `<integration>` is one of:
- `claude` - Claude Code hooks
- `none` - No AI integration (docs only)
- Future: `cursor`, `copilot`, etc.

### Fresh Setup Flow

```
$ dydo init claude

DynaDocs Setup
──────────────

Your name: balazs
Number of agents [26]: 5

Creating...
  ✓ dydo.json
  ✓ CLAUDE.md (entry point)
  ✓ dydo/ structure with workflows
  ✓ Added dydo/agents/ to .gitignore
  ✓ Claude Code hooks configured

Agents assigned to balazs: Adele, Brian, Charlie, Dexter, Emma

Documentation funnel created:
  CLAUDE.md → dydo/index.md → dydo/agents/{name}/workflow.md → must-reads

Next steps:
  1. Set environment variable: export DYDO_HUMAN=balazs
  2. Customize dydo/understand/about.md for your project
  3. Customize dydo/understand/architecture.md for technical structure
  4. Customize dydo/guides/coding-standards.md for code conventions
```

### Join Flow (Teammate)

```
$ dydo init claude --join

Joining existing DynaDocs project...

Your name: alice
Number of agents [5]: 5

  ✓ Updated dydo.json (added alice to assignments)
  ✓ Created local workspace (dydo/agents/)
  ✓ Claude Code hooks configured

Agents assigned to alice: Frank, Grace, Henry, Iris, Jack

Set your environment variable:
  export DYDO_HUMAN=alice
```

### Idempotent Behavior

Running `dydo init claude` multiple times:
- Does NOT overwrite existing `dydo.json`
- Does NOT overwrite existing docs
- DOES update hooks (merge, not replace)
- DOES create missing agent workspaces

---

## Check Command Enhancements

`dydo check` validates both documentation and agent configuration:

```
$ dydo check

Checking dydo/ documentation...
  ✓ 24 files checked
  ⚠ dydo/guides/backend/api.md: Missing summary paragraph

Checking agent assignments...
  ⚠ dydo/agents/Adele/state.md says assigned: bob
    but dydo.json assigns Adele to balazs
  ⚠ Agent "Frank" in dydo.json but no workspace exists
    Run: dydo workspace init
  ⚠ Stale session: dydo/agents/Brian/.session references PID 12345 (not running)

Found 0 errors, 4 warnings
```

### Agent-Related Checks

- State file `assigned` field matches `dydo.json` assignments
- All agents in `dydo.json` have workspaces
- No orphaned workspaces (folder exists but agent not in config)
- Stale sessions (PID no longer running)

---

## Cross-Human Dispatch

When Agent A (owned by balazs) needs to hand off work to alice:

```bash
dydo dispatch --role reviewer --task jwt-auth --to alice --brief "Review JWT impl"
```

This creates a **task file** (not a local inbox item):

**dydo/project/tasks/jwt-auth.md:**
```markdown
---
name: jwt-auth
status: review-pending
assigned-human: alice
requested-by: balazs
requested-at: 2025-01-28T10:30:00Z
---

# Task: jwt-auth

## Review Request

From: balazs (via Adele)
Role: reviewer
Brief: Review JWT impl

## Files
- src/Auth/JwtService.cs
- src/Auth/AuthMiddleware.cs
```

Alice's agents see it via `dydo task list`.

**Same-human dispatch** uses local inbox (fast, no git commit needed).

---

## Documentation Philosophy

### JITI - Just In Time Information

DynaDocs uses **JITI** (Just In Time Information) - progressive disclosure of information through a documentation funnel:

```
CLAUDE.md (project root)
    │
    └── dydo/index.md (entry point)
            │
            ├── agents/{name}/workflow.md (agent-specific entry, gitignored)
            │       └── Must-reads (about, architecture, coding-standards)
            │
            ├── understand/_index.md (hub)
            │       ├── about.md (project overview - first thing agents read)
            │       ├── architecture.md (technical structure)
            │       └── {concept}.md (domain concepts)
            │
            ├── guides/_index.md (hub)
            │       ├── coding-standards.md (code conventions)
            │       ├── how-to-use-docs.md (docs navigation guide)
            │       └── {task}.md (how-to guides)
            │
            ├── reference/_index.md (hub)
            │       ├── writing-docs.md (documentation conventions)
            │       └── {spec}.md (API specs, config docs)
            │
            └── project/_index.md (hub)
                    ├── decisions/ (ADRs)
                    ├── pitfalls/ (gotchas)
                    └── changelog/ (what changed)
```

**Key principle:** AI agents follow the funnel: `CLAUDE.md` → `index.md` → their workflow file → must-reads. Information is disclosed progressively, not all at once.

### Graph Connectivity (Lateral)

Related docs link to each other bidirectionally:

```
authentication.md ◄──────────► glossary.md#jwt
    │
    └──────────────────► 001-auth-strategy-decision.md
```

### Hub Files (`_index.md`)

Every folder with multiple docs needs an `_index.md`:

```markdown
---
area: general
type: hub
---

# Backend Guides

Guides for working with the C# backend, organized by subsystem.

## Contents

- [API Patterns](./api-patterns.md) - Minimal API conventions
- [Database](./database.md) - EF Core, migrations, queries
- [Background Jobs](./background-jobs.md) - Job scheduling
```

### Clustering Rule

**When a folder exceeds ~7-10 items, create subfolders with their own `_index.md`.**

---

## Validation Rules

### Rule 1: Naming Convention (kebab-case)

All file and folder names must be kebab-case (lowercase, hyphens).

**Valid:** `coding-standards.md`, `_index.md`, `understand/`

**Invalid:** `Coding Standards.md`, `CodingStandards.md`, `coding_standards.md`

**Exceptions:** `CLAUDE.md`, `.gitkeep`

**Auto-fix:** Yes - rename and update references

### Rule 2: Relative Links Only

All internal links must be relative paths in standard markdown format.

**Valid:** `[text](./path/to/file.md)`, `[text](../glossary.md#term)`

**Invalid:** `[[wikilink]]`, `[text](/absolute/path.md)`

**Auto-fix:** Yes - convert wikilinks to relative paths

### Rule 3: Frontmatter Required

Every doc must have YAML frontmatter:

```markdown
---
area: understand | guides | reference | general | frontend | backend | microservices | platform
type: context | concept | guide | reference | hub | decision | pitfall | changelog
---
```

**Auto-fix:** No - requires human judgment

### Rule 4: Summary Required

First paragraph after title must be 1-3 sentence summary.

**Auto-fix:** No - report as warning

### Rule 5: No Broken Links

All internal links must point to existing files/anchors.

**Auto-fix:** No - report as error

### Rule 6: Hub Files Required

Every folder with docs must have `_index.md`.

**Auto-fix:** Yes - creates hub file with auto-generated links to sibling documents (sorted alphabetically, with titles and descriptions extracted from each file)

### Rule 7: No Orphan Docs

Every doc must be reachable from `index.md`.

**Auto-fix:** No - report as warning

### Rule 8: Folder Meta Files Required

Direct children of `guides/`, `project/`, `reference/`, and `understand/` must have a meta file named `_foldername.md` (e.g., `guides/api/` needs `_api.md`).

**Purpose:** Provides folder descriptions used in hub links and ensures empty folders are tracked by git.

**Auto-fix:** Yes - creates scaffold with placeholder content

---

## Project Structure (Tool)

```
DynaDocs/
├── DynaDocs.csproj
├── Program.cs
│
├── Commands/
│   ├── AgentCommand.cs         # claim, release, status, list, role, new, rename, remove, reassign
│   ├── CheckCommand.cs
│   ├── CleanCommand.cs
│   ├── DispatchCommand.cs
│   ├── FixCommand.cs
│   ├── GraphCommand.cs
│   ├── GuardCommand.cs
│   ├── IndexCommand.cs
│   ├── InboxCommand.cs
│   ├── InitCommand.cs
│   ├── ReviewCommand.cs
│   ├── TaskCommand.cs
│   ├── WhoamiCommand.cs
│   └── WorkspaceCommand.cs
│
├── Models/
│   ├── AgentsConfig.cs
│   ├── AgentSession.cs
│   ├── AgentState.cs
│   ├── AgentStatus.cs
│   ├── DocFile.cs
│   ├── DydoConfig.cs
│   ├── Frontmatter.cs
│   ├── HookInput.cs
│   ├── InboxItem.cs
│   ├── LinkInfo.cs
│   ├── PresetAgentNames.cs
│   ├── StructureConfig.cs
│   ├── TaskFile.cs
│   └── Violation.cs
│
├── Services/
│   ├── AgentRegistry.cs
│   ├── ConfigService.cs
│   ├── DocGraph.cs
│   ├── DocScanner.cs
│   ├── FolderScaffolder.cs
│   ├── IndexGenerator.cs
│   ├── LinkResolver.cs
│   ├── MarkdownParser.cs
│   ├── ProcessUtils.cs
│   └── TemplateGenerator.cs
│
├── Rules/
│   ├── BrokenLinksRule.cs
│   ├── FolderMetaFilesRule.cs
│   ├── FrontmatterRule.cs
│   ├── HubFilesRule.cs
│   ├── IRule.cs
│   ├── NamingRule.cs
│   ├── OrphanDocsRule.cs
│   ├── RelativeLinksRule.cs
│   └── SummaryRule.cs
│
├── Templates/                   # Template files for generation
│   ├── about.template.md                 # Project overview (understand/)
│   ├── agent-states.template.md          # Agent state file format
│   ├── agent-workflow.template.md        # Agent entry point
│   ├── architecture.template.md          # Architecture doc (understand/)
│   ├── changelog.template.md             # Changelog entry format
│   ├── coding-standards.template.md      # Coding standards (guides/)
│   ├── decision.template.md              # ADR format
│   ├── dydo-commands.template.md         # CLI reference (reference/)
│   ├── files-off-limits.template.md      # Protected files doc
│   ├── glossary.template.md              # Glossary format
│   ├── how-to-use-docs.template.md       # Docs navigation (guides/)
│   ├── index.template.md                 # Root index
│   ├── pitfall.template.md               # Pitfall format
│   ├── welcome.template.md               # Welcome page
│   ├── writing-docs.template.md          # Documentation conventions (reference/)
│   ├── mode-code-writer.template.md      # Code-writer role guidance
│   ├── mode-co-thinker.template.md       # Co-thinker role guidance
│   ├── mode-docs-writer.template.md      # Docs-writer role guidance
│   ├── mode-interviewer.template.md      # Interviewer role guidance
│   ├── mode-planner.template.md          # Planner role guidance
│   ├── mode-reviewer.template.md         # Reviewer role guidance
│   └── mode-tester.template.md           # Tester role guidance
│
└── Utils/
    ├── ConsoleOutput.cs
    ├── ExitCodes.cs
    └── PathUtils.cs
```

### Dependencies (NuGet)

```xml
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
<PackageReference Include="Markdig" Version="0.34.0" />
<PackageReference Include="YamlDotNet" Version="15.1.0" />
```

---

## Example Workflow

### Full Feature Workflow (Multi-Agent)

```
Human: Sets DYDO_HUMAN=balazs, runs "claude --feature A"

Terminal A (Adele - owned by balazs):
  1. dydo agent claim auto         # Claims Adele
  2. dydo agent role interviewer
  3. Interviews human, writes brief
  4. dydo agent role planner
  5. Creates plan
  6. dydo task create jwt-auth
  7. dydo agent role code-writer
  8. Implements feature
  9. dydo task ready-for-review jwt-auth --summary "..."
  10. dydo dispatch --role reviewer --task jwt-auth
      → Claims Brian (also owned by balazs), launches terminal

Terminal B (Brian - owned by balazs):
  1. dydo agent claim auto         # Claims Brian
  2. dydo inbox show               # Sees review request
  3. dydo agent role reviewer
  4. Reviews code (read-only)
  5. dydo review complete jwt-auth --status pass
  6. dydo agent release

Human reviews, approves: dydo task approve jwt-auth
Feature complete!
```

### Cross-Human Handoff

```
balazs finishes implementation, needs alice's team to review:

Terminal A (Adele - owned by balazs):
  1. dydo task ready-for-review jwt-auth --summary "..."
  2. dydo dispatch --role reviewer --task jwt-auth --to alice
     → Creates task file in dydo/project/tasks/
     → Does NOT launch terminal (different human)

Later, alice checks pending tasks:
  $ dydo task list
  jwt-auth    review-pending    reviewer    balazs→alice

Terminal F (Frank - owned by alice):
  1. dydo agent claim auto         # Claims Frank
  2. dydo task list                # Sees jwt-auth
  3. dydo agent role reviewer --task jwt-auth
  4. Reviews code
  5. dydo review complete jwt-auth --status pass
  6. dydo agent release
```

---

## Implementation Notes

### Process Tree Walking

To identify which terminal owns which agent:

```
Terminal (PowerShell) ─── PID: 1000
    └── Claude Code ───── PID: 1001
          └── Hook (dydo) ─ PID: 1002
```

When `dydo guard` runs (PID 1002), it walks up to find terminal PID (1000), then searches `.session` files for a match.

### Glob Pattern Matching

Permission patterns use glob syntax:
- `**` matches any path
- `*` matches within a single segment
- `{self}` is replaced with agent name

```csharp
// Example: .workspace/{self}/** with agent "Adele"
// Becomes: .workspace/Adele/**
```

### State File Format

Agent state is stored as markdown with YAML frontmatter for human readability:

```markdown
---
agent: Adele
role: code-writer
task: jwt-auth
status: working
assigned: balazs
started: 2025-01-28T10:30:00Z
allowed-paths: ["src/**", "tests/**"]
denied-paths: ["dydo/**", "project/**"]
task-role-history: {"jwt-auth": ["planner", "code-writer"]}
---

# Adele — Session State

## Current Task

jwt-auth

## Progress

- [x] Created JwtService
- [ ] Add refresh token support

## Decisions Made

- Using RS256 for token signing

## Blockers

(None)
```

The `task-role-history` field tracks which roles an agent has held on each task. This enables the self-review prevention system - an agent cannot become `reviewer` on a task where they were previously `code-writer`.

---

## Future Enhancements (Not for v1)

1. **Watch mode**: `dydo watch` - Continuously validate on file changes
2. **Pre-commit hook**: Validate docs before commit
3. **Graph visualization**: Mermaid/DOT export of doc connections
4. **Staleness detection**: Warn if docs not updated in N months
5. **Coverage report**: Which areas have good docs vs sparse
6. **Auto-clustering**: Suggest when folders should be split
7. **GitHub/Slack integration**: Notifications for cross-human dispatch

---

## Summary

DynaDocs (`dydo`) is a C# console tool that:

1. **Validates** docs against naming, linking, and structure rules
2. **Auto-fixes** what it can (renames, link conversion, skeleton hubs, folder meta files)
3. **Generates** index.md from doc structure
4. **Queries** the doc graph for context gathering
5. **Orchestrates** multi-agent workflows with role-based permissions
6. **Supports multi-user** collaboration via agent assignments
7. **Prevents self-review** through task role history tracking
8. **Supports template customization** via project-local `_system/templates/`
9. **Enforces staged access** - agents must claim identity and role before accessing files
10. **Folder meta files** for self-documenting folder purposes

### Key Principles

1. **Dy + Do**: Documentation and orchestration in one unified `dydo/` folder
2. **JITI**: Just In Time Information - progressive disclosure via documentation funnel
3. **Platform-agnostic**: Hook system works with Claude, future AI tools
4. **Multi-user**: Each human gets assigned agents, no conflicts
5. **Objective outputs**: Command output suitable for both humans and AI
6. **Local state, shared docs**: Agent workspaces gitignored, templates committed
7. **Hierarchical navigation**: CLAUDE.md → Index → Agent workflow → Must-reads → Details
8. **Graph connectivity**: Related docs link bidirectionally
9. **Kebab-case everywhere**: Consistent, parseable filenames
10. **No self-review**: Code-writer cannot become reviewer on same task (fresh eyes validation)
11. **Customizable templates**: Project-local `_system/templates/` overrides built-in defaults
12. **Staged access control**: Progressive file access unlocks as agents claim identity and set role
13. **Fail-closed security**: Guard blocks operations without identity/role (doesn't just warn)