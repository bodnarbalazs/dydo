# DynaDocs (dydo) - Complete Specification v2

This document contains everything needed to build the DynaDocs tool from scratch.

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
│
└── dydo/                        # Main DynaDocs folder
    ├── index.md                 # Entry point (committed)
    ├── glossary.md              # Term definitions (committed)
    │
    │── understand/              # ┐
    │── guides/                  # │ The "Dy" - Documentation
    │── reference/               # │ (committed to git)
    │── project/                 # │
    │   ├── tasks/               # │ ← Cross-human dispatch
    │   ├── decisions/           # │
    │   ├── pitfalls/            # │
    │   └── changelog/           # ┘
    │
    └── agents/                  # The "Do" - Orchestration (GITIGNORED)
        ├── agent-states.md      # Central registry
        ├── Adele/
        │   ├── state.md         # Role, task, permissions
        │   ├── .session         # PID tracking
        │   ├── workflow.md      # Agent instructions
        │   ├── inbox/           # Messages from other agents
        │   └── scratch/         # Working notes
        ├── Brian/
        └── ... (all agents)
```

### What Gets Committed vs Gitignored

| Path | Committed | Why |
|------|-----------|-----|
| `dydo.json` | Yes | Team configuration |
| `dydo/` (except agents/) | Yes | Documentation is shared truth |
| `dydo/project/tasks/` | Yes | Cross-human task handoff |
| `dydo/agents/` | **No** | Local per-machine state |

**.gitignore entry:**
```
dydo/agents/
```

### Documentation Folder Purposes

| Folder | Question it answers | Content type |
|--------|---------------------|--------------|
| `understand/` | "What IS this?" | Domain concepts, business logic, architecture |
| `guides/` | "How do I DO this?" | Step-by-step task instructions |
| `reference/` | "What are the specs?" | API docs, config options, tool docs |
| `project/` | "Why/how do we work?" | Decisions, pitfalls, changelog, tasks |

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

**Block action:**
- Exit code: `2`
- Stderr: Error message explaining why blocked

Example stderr messages:
```
Agent Adele (reviewer) cannot edit src/Auth.cs. Reviewer role has no write permissions.
Agent Frank is assigned to human 'alice', not 'balazs'. Cannot perform action.
No agent identity assigned to this process. Run 'dydo agent claim auto' first.
```

### Claude Code Hook Configuration

When `dydo init claude` runs, it creates/updates `.claude/settings.local.json`:

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write",
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

---

## Agent Roles and Permissions

| Role | Can Edit | Cannot Edit |
|------|----------|-------------|
| `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
| `reviewer` | (nothing - read only) | `**` |
| `docs-writer` | `dydo/**` (except agents/) | `src/**`, `tests/**` |
| `interviewer` | `dydo/agents/{self}/**` | Everything else |
| `planner` | `dydo/agents/{self}/**`, `dydo/project/tasks/**` | `src/**` |

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
  ✓ dydo/ folder structure
  ✓ dydo/index.md
  ✓ dydo/understand/_index.md
  ✓ dydo/guides/_index.md
  ✓ dydo/reference/_index.md
  ✓ dydo/project/_index.md
  ✓ Added dydo/agents/ to .gitignore
  ✓ Claude Code hooks configured (.claude/settings.local.json)

Agents assigned to balazs: Adele, Brian, Charlie, Dexter, Emma

Start with:
  export DYDO_HUMAN=balazs
  claude --feature A "Your task description"
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

Alice's agents see it via `dydo task list --mine`.

**Same-human dispatch** uses local inbox (fast, no git commit needed).

---

## Documentation Philosophy

### Hierarchical Navigation (Top-Down)

```
index.md (entry point)
    │
    ├── understand/_index.md (hub)
    │       ├── platform.md (detail)
    │       └── content/_index.md (sub-hub)
    │               ├── teases.md (detail)
    │               └── assets.md (detail)
    │
    ├── guides/_index.md (hub)
    │       └── backend/_index.md (sub-hub)
    │               └── database.md (detail)
    │
    └── project/_index.md (hub)
            └── decisions/_index.md (sub-hub)
                    └── 001-clean-architecture.md (detail)
```

**Key principle:** Index.md only links to top-level hubs. Hubs link to their children.

### Graph Connectivity (Lateral)

Related docs link to each other bidirectionally:

```
teases.md ◄──────────► glossary.md#tease
    │
    └──────────────────► 001-tease-structure-decision.md
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
area: frontend | backend | microservices | platform | general
type: hub | concept | guide | reference | decision | pitfall | changelog
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

**Auto-fix:** Yes - create skeleton

### Rule 7: No Orphan Docs

Every doc must be reachable from `index.md`.

**Auto-fix:** No - report as warning

---

## Project Structure (Tool)

```
DynaDocs/
├── DynaDocs.csproj
├── Program.cs
│
├── Commands/
│   ├── CheckCommand.cs
│   ├── FixCommand.cs
│   ├── IndexCommand.cs
│   ├── GraphCommand.cs
│   ├── InitCommand.cs
│   ├── WhoamiCommand.cs        # NEW
│   ├── AgentCommand.cs
│   ├── DispatchCommand.cs
│   ├── InboxCommand.cs
│   ├── GuardCommand.cs
│   ├── ReviewCommand.cs
│   ├── TaskCommand.cs
│   ├── CleanCommand.cs
│   └── WorkspaceCommand.cs
│
├── Models/
│   ├── DydoConfig.cs           # NEW
│   ├── HookInput.cs            # NEW
│   ├── DocFile.cs
│   ├── Frontmatter.cs
│   ├── Violation.cs
│   ├── LinkInfo.cs
│   ├── AgentState.cs
│   ├── AgentSession.cs
│   ├── InboxItem.cs
│   └── TaskFile.cs
│
├── Services/
│   ├── ConfigService.cs        # NEW
│   ├── DocScanner.cs
│   ├── LinkResolver.cs
│   ├── MarkdownParser.cs
│   ├── IndexGenerator.cs
│   ├── DocGraph.cs
│   ├── AgentRegistry.cs
│   └── ProcessUtils.cs
│
├── Rules/
│   ├── IRule.cs
│   ├── NamingRule.cs
│   ├── RelativeLinksRule.cs
│   ├── FrontmatterRule.cs
│   ├── SummaryRule.cs
│   ├── BrokenLinksRule.cs
│   ├── HubFilesRule.cs
│   └── OrphanDocsRule.cs
│
└── Utils/
    ├── PathUtils.cs
    ├── ConsoleOutput.cs
    └── ExitCodes.cs
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
  $ dydo task list --mine
  jwt-auth    review-pending    reviewer    balazs→alice

Terminal F (Frank - owned by alice):
  1. dydo agent claim auto         # Claims Frank
  2. dydo task list --mine         # Sees jwt-auth
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
2. **Auto-fixes** what it can (renames, link conversion, skeleton hubs)
3. **Generates** index.md from doc structure
4. **Queries** the doc graph for context gathering
5. **Orchestrates** multi-agent workflows with role-based permissions
6. **Supports multi-user** collaboration via agent assignments

### Key Principles

1. **Dy + Do**: Documentation and orchestration in one unified `dydo/` folder
2. **Platform-agnostic**: Hook system works with Claude, future AI tools
3. **Multi-user**: Each human gets assigned agents, no conflicts
4. **Objective outputs**: Command output suitable for both humans and AI
5. **Local state, shared docs**: Agent workspaces gitignored, docs committed
6. **Hierarchical navigation**: Index → Hubs → Details
7. **Graph connectivity**: Related docs link bidirectionally
8. **Kebab-case everywhere**: Consistent, parseable filenames