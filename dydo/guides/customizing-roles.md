---
area: guides
type: guide
---

# Customizing Roles

The skill template is the role: `dydo sync` discovers `skill-<name>.template.md` sources and compiles
their methodology into native skills and, for worker roles, spawnable agent definitions. Role methods
receive Linear Issue/Project context from the host; they do not create a repository work hierarchy.

---

## Creating a custom role

1. Create `dydo/_system/templates/skill-<name>.template.md`. Frontmatter declares the metadata; the body is the methodology:

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

2. Run `dydo sync`. The role compiles into `.claude/skills/<name>/` (and
   `.claude/agents/<name>.md` if `emit: agent`), plus the Codex agent and shared-skill surfaces.

## Overriding a built-in role

Copy the shipped template into `dydo/_system/templates/` and edit it — project-local templates shadow the built-ins. `dydo template update` refreshes only un-customized files on upgrade; your overrides are left alone.

## Skill resources

Per-domain reference files use `<role>-resource-<name>.template.md` and compile to the skill's
`resources/<name>.md`. The reviewer ships rubrics for code, plans, integrated Project delivery, docs,
and tests. Some compatibility filenames retain older wording; the compiled content and current method
are authoritative.

## What the compiler reads

| Frontmatter key | Effect |
|---|---|
| `mode` | The role's name (must match the filename) |
| `description` | The compiled skill/agent description |
| `emit` | `agent` → worker (agent + skill); `skill` → in-session methodology only |
| `read-only` | `true` → compiled agent gets no Edit/Write tools |

The body compiles into the skill's methodology. Model tiers are bound separately in `dydo.json`
(`models.roles`). Change the source and re-run `dydo sync`; never edit compiled artifacts directly.

## Related

- [dydo-glossary.md](../reference/dydo-glossary.md) — role vs skill vs agent, precisely
- [dydo-commands.md](../reference/dydo-commands.md) — `dydo sync`, `dydo template update`
