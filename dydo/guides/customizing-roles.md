---
area: guides
type: guide
---

# Customizing Roles

The mode template **is** the role. There is no separate role registry: `dydo sync` discovers roles by enumerating `mode-<name>.template.md` files — the built-in set plus anything you drop into `dydo/_system/templates/` — and compiles each into your platform's skill (and agent, for workers).

---

## Creating a custom role

1. Create `dydo/_system/templates/mode-<name>.template.md`. Frontmatter declares the metadata; the body is the methodology:

   ```markdown
   ---
   mode: data-migrator
   description: Plans and executes schema and data migrations safely.
   emit: agent            # agent = spawnable worker (agent + skill); skill = in-session methodology only
   read-only: false       # true → the compiled agent gets no Edit/Write tools
   ---

   # Data Migrator

   Your job: ...

   ## Mindset
   ...

   ## Work
   ...
   ```

2. Run `dydo sync`. The role compiles into `.claude/skills/<name>/` (and `.claude/agents/<name>.md` if `emit: agent`), plus the Codex mirrors.

## Overriding a built-in role

Copy the shipped template into `dydo/_system/templates/` and edit it — project-local templates shadow the built-ins. `dydo template update` refreshes only un-customized files on upgrade; your overrides are left alone.

## Skill resources

Per-domain reference files ride the same convention: `<role>-resource-<name>.template.md` in `dydo/_system/templates/` compiles to the skill's `resources/<name>.md`. The reviewer's per-target rubrics (code, plan, merge-sprint, docs, tests) are the shipped example — add your own targets the same way.

## What the compiler reads

| Frontmatter key | Effect |
|---|---|
| `mode` | The role's name (must match the filename) |
| `description` | The compiled skill/agent description |
| `emit` | `agent` → worker (agent + skill); `skill` → in-session methodology only |
| `read-only` | `true` → compiled agent gets no Edit/Write tools |

The body compiles into the skill's methodology; `## Must-Reads` links become the compiled agent's read-first list. Model tiers are bound separately in `dydo.json` (`models.roles`).

## Related

- [dydo-glossary.md](../reference/dydo-glossary.md) — role vs skill vs agent, precisely
- [dydo-commands.md](../reference/dydo-commands.md) — `dydo sync`, `dydo template update`
