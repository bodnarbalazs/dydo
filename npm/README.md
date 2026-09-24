# DynaDocs (dydo)

Own your project's durable knowledge, use Linear for live work, and author shared methods as native
skills for Claude Code and Codex.

dydo is a documentation, skill-authoring, and guardrail framework for AI coding assistants. It keeps
reviewed project knowledge in Git, authors each role as a native skill folder, and applies
project rules through hooks. Linear owns live Projects and Issues; Claude Code and Codex own agent
identity, delegation, scheduling, and worktree isolation.

## What it provides

- A structured knowledge tree for architecture, decisions, guides, plans, audits, changelog, and
  FutureFeature ideas.
- One canonical `skills/<category>/<name>/` tree (`roles/officers/`, `roles/crew/`, `engineering/`,
  `productivity/`), walked by rule rather than depth and exposed flat through host-native discovery
  paths (no category level) by the dependency-free `setup-skills.mjs`, with no compile step. Both
  live in the dydo repository, not in this package.
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
dydo check            # validate documentation
dydo fix              # repair supported documentation problems
```

Fill in `dydo/understand/about.md` and `dydo/understand/architecture.md`, then adapt
`dydo/guides/coding-standards.md` and `dydo.json`. Use `dydo init <integration> --join` when wiring
another runtime or machine into an existing project.

## Skills

The skills do not come with this package, and `dydo init` does not write them. Before the first
agent session, copy `skills/`, `setup-skills.mjs` and `THIRD-PARTY-NOTICES.md` (the MIT notices of
the adapted skills travel with them) from the
[dydo repository](https://github.com/bodnarbalazs/dydo) into the project root, commit them, and run
`node setup-skills.mjs`. `dydo init` and every `--join` add `/.claude/skills/` to `.gitignore` when
Claude Code is wired and `/.agents/skills/` when Codex is, so the projections stay local. Edit the
canonical `skills/<category>/<name>/` folder directly; there is no compile step or automatic
reconciliation. OpenCode may read the same two folders; that is untested, and dydo has no OpenCode
init mode.

## Commands

| Command | Purpose |
|---|---|
| `dydo init <integration>` | Scaffold or join a project. |
| `dydo check`, `dydo fix`, `dydo index`, `dydo graph` | Maintain the documentation tree. |
| `dydo guard` | Evaluate hook rules and nudges. |
| `dydo validate` | Validate local configuration and nudges. |
| `dydo gap-check` | Run the test runner configured in `dydo.json`. |
| `dydo completions`, `version`, `help` | Shell and utility commands. |

See the full [command reference](https://github.com/bodnarbalazs/dydo/blob/master/dydo/reference/dydo-commands.md).

## License

MIT — see LICENSE.
