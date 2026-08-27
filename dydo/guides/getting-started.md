---
area: guides
type: guide
---

# Getting Started

Install dydo, initialize the durable knowledge tree, compile native skills, and connect the resulting
project context to work managed in Linear.

## Prerequisites

- a Git repository for durable knowledge and reviewable proof;
- Claude Code or Codex for agent execution;
- Linear access when the project tracks live work.

## 1. Install

```bash
npm install -g dydo
# or
dotnet tool install -g dydo
```

## 2. Initialize

```bash
dydo init codex
# or: dydo init claude / dydo init all / dydo init none
```

Every mode creates the documentation tree, source templates, and `CLAUDE.md`. The `claude`, `codex`, and
`all` modes wire guard hooks only for the selected runtimes; Codex selections also add `AGENTS.md`. The
`none` mode installs no guard hooks and no `AGENTS.md`. Initialization creates durable Decisions,
changelog, pitfalls, and FutureFeature idea documentation. It does not scaffold repository folders for
live work; create and manage actionable work in Linear.

## 3. Compile native methods

```bash
dydo sync
```

The product compiles role templates and resources into platform-native skills and worker agents, plus
supported workflows. Re-run this command after changing a source template.

## 4. Fill in durable context

Start with:

- `dydo/understand/about.md` — purpose and domain;
- `dydo/understand/architecture.md` — components and boundaries;
- `dydo/guides/coding-standards.md` — repository conventions.

Then validate:

```bash
dydo check
dydo fix
```

## 5. Run work through Linear

1. Shape intent and record any durable Decision.
2. Create an appropriately sized Linear Issue. For coordinated or architecture-sensitive work, link the
   Linear Project and Issues to a reviewed repository Project plan.
3. Execute the Issue through the host runtime in an isolated branch or worktree.
4. Attach governing commit, test, review, and delivery evidence to the Issue.
5. Independently review each implementation Issue; audit the integrated Project against its plan.
6. Flow durable knowledge back into Git rather than leaving it only in comments or sessions.

An atomic, autonomous-ready Issue can be its own reviewed contract. Complexity, not a repository record
hierarchy, determines when a separate Project plan is necessary.

## Joining an existing project

```bash
dydo init codex --join
# or
dydo init claude --join
```

Join wires the local runtime without replacing the existing documentation tree.

## Related

- [DynaDocs](../reference/about-dynadocs.md)
- [Customizing Roles](./customizing-roles.md)
- [Configuration](../reference/configuration.md)
- [Work Model](../understand/work-model.md)
