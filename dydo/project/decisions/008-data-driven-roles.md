---
type: decision
status: accepted
date: 2026-03-10
area: project
---

# 008 — Data-Driven Role Definitions

Replace hardcoded role permissions and constraints with JSON role definition files. Default roles use the same format as custom roles — dogfooding by design.

## Problem

Roles are hardcoded in `AgentRegistry.BuildRolePermissions()` as a C# dictionary, with constraints (self-review prevention, orchestrator graduation) as special-case if-statements in `CanTakeRole()`. This means:

1. Users cannot define custom roles without modifying C# source.
2. Adding a role requires code changes in multiple places (permission map, constraint logic, guard denial messages, template generation).
3. The system can't validate its own role definitions — they're compiled in.

## Decision

### Role definitions are JSON files

Each role is a `*.role.json` file in `dydo/_system/roles/`, defined by a C# `RoleDefinition` class (source-generated JSON, AOT-compatible). The file captures: name, description, writable/read-only path patterns, constraints with authored error messages, and a reference to the mode template file.

Default roles ship as hardcoded C# `RoleDefinition` objects. `dydo init` serializes them to JSON files. At runtime, the guard reads the JSON files — never the C# definitions directly.

### JSON over markdown for role files

Role definitions are highly structured data with strict typing (path lists, constraint types, boolean flags). Markdown frontmatter is designed for unstructured documents with light metadata. Forcing structured schemas into frontmatter fights the format. JSON matches the data shape.

### Named path sets in dydo.json

Path variables (`{source}`, `{tests}`) are generalized into named path sets defined in `dydo.json`. Role files reference these by name. Users can define custom path sets for custom roles (e.g., `{infra}`, `{config}`). This keeps project-level path knowledge in one place rather than duplicated across role files.

### Composable constraints from hardcoded building blocks

Constraints are composed from a fixed set of evaluable condition types (e.g., `role-transition`, `requires-prior`). The building blocks are hardcoded evaluators in C#. Role files compose them with parameters and an authored error message. The message is never generated — whoever defines the constraint writes the exact text the agent sees, with variable substitution (`{agent}`, `{task}`, `{current_role}`).

This replaces the current special-case if-statements while keeping error messages specific and contextual.

### Must-reads stay in templates

Must-read enforcement is unchanged. The mode template links to files with `must-read: true` in their frontmatter. The guard checks these. The role definition file does not own must-reads — the template does.

### Reset command

`dydo roles reset` regenerates base role files from the hardcoded C# definitions. Custom roles are preserved. `dydo roles reset --all` removes everything and regenerates only base roles. Base roles are identified by a `base: true` flag in the JSON. Human-only command.

### Workflow template role table is dynamic

The workflow template uses a `{{ROLE_TABLE}}` placeholder. Template generation reads all role files and builds the table. Custom roles automatically appear in the workflow agents see.

## Implications

- `BuildRolePermissions()` becomes a loader, not a definition site.
- The hardcoded constraint if-statements in `CanTakeRole()` are replaced by data-driven evaluation.
- `dydo.json` schema expands to include `pathSets`.
- New commands: `dydo roles reset`, `dydo roles create`, `dydo validate`.
- Validation runs automatically on role creation and on first guard action per day.
- A comprehensive behavioral test suite must be written before any refactoring begins — the test suite is the regression contract.
