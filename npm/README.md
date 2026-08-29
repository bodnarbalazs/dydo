# DynaDocs (dydo)

Own your project's durable knowledge, use Linear for live work, and compile shared methods for Claude
Code and Codex.

dydo is a documentation, skill-authoring, and guardrail framework for AI coding assistants. It keeps
reviewed project knowledge in Git, compiles one role source into native runtime artifacts, and applies
project rules through hooks. Linear owns live Projects and Issues; Claude Code and Codex own agent
identity, delegation, scheduling, and worktree isolation.

## What it provides

- A structured knowledge tree for architecture, decisions, guides, plans, audits, changelog, and
  FutureFeature ideas.
- `dydo sync` to compile shared role, resource, and workflow sources into native Claude Code and Codex
  artifacts.
- `dydo guard` to apply off-limits paths, dangerous-command checks, and configurable nudges.
- `dydo check`, `dydo fix`, `dydo index`, and `dydo graph` to maintain the documentation graph.

dydo does not create, update, poll, cache, or mirror Linear objects. Put current work in Linear and keep
only durable knowledge and reviewed proof in Git.

## Installation

```bash
npm install -g dydo
# or
dotnet tool install -g dydo
```

## Quick start

Run from a project root:

```bash
dydo init codex       # or: claude, all, none
dydo sync             # compile roles and skills
dydo check            # validate documentation
dydo fix              # repair supported documentation problems
```

Fill in `dydo/understand/about.md` and `dydo/understand/architecture.md`, then adapt
`dydo/guides/coding-standards.md` and `dydo.json`. Use `dydo init <integration> --join` when wiring
another runtime or machine into an existing project.

Do not hand-edit compiled `.claude/`, `.codex/`, or `.agents/skills/` artifacts. Change the source
templates and run `dydo sync`.

## Commands

| Command | Purpose |
|---|---|
| `dydo init <integration>` | Scaffold or join a project. |
| `dydo sync` | Compile shared roles, resources, and workflows. |
| `dydo check`, `dydo fix`, `dydo index`, `dydo graph` | Maintain the documentation tree. |
| `dydo guard` | Evaluate hook rules and nudges. |
| `dydo template update` | Update framework-owned templates and docs. |
| `dydo validate` | Validate local configuration and nudges. |
| `dydo model cap`, `uncap`, `status` | Manage temporary native model-tier caps. |
| `dydo completions`, `version`, `help` | Shell and utility commands. |

See the full [command reference](https://github.com/bodnarbalazs/dydo/blob/master/dydo/reference/dydo-commands.md).

## License

MIT — see LICENSE.
