---
area: guides
type: guide
---

# Troubleshooting

Common guard, validation, template, and work-model failures, with the narrow recovery for each.

## Guard blocks

### Off-limits path

The path matches `dydo/files-off-limits.md`. Add a narrowly justified whitelist entry only when the
project truly needs agent access; otherwise leave the protected file alone.

### Dangerous command

The command matched an unconditional destructive pattern. There is no guard override; resolve the exact
target and issue a narrower operation.

### Chained directory change

Run the command with the tool's working-directory option or change directory separately. Chaining
`cd` can defeat safe command approval.

### Warning asks for a retry

A `warn` nudge makes the first attempt pause. Read the message and repeat only when the operation is
still correct. A `block` nudge does not permit retry.

### Indirect dydo invocation

Call `dydo` directly from `PATH`; do not wrap it in `npx`, `dotnet run`, Python, or another shell.

## Reviewed-intent failures

Implementation needs an atomic, autonomous-ready Linear Issue or a Linear Issue linked to the reviewed
Project plan governing coordinated work. If the contract is missing, ambiguous, or contradicts the
repository, stop and ask the coordinator or human instead of inventing local work records.

## Validation failures

```bash
dydo check
dydo check dydo/guides/
dydo fix
```

| Error | Recovery |
|---|---|
| Missing frontmatter | Add the required `area` and `type` fields. |
| Missing title | Add an H1 title. An opening summary is optional. |
| Bad filename | Rename to kebab-case, or let `dydo fix` handle a safe rename. |
| Broken link | Correct or remove the relative target. |
| Unreachable document | Add a useful authored navigation or durable-parent link. |

The dydo 2.x PM corpus has been migrated and retired. Use frozen Git commit permalinks when historical
evidence is needed; do not recreate repository work records to address a current problem.

## Skill setup collision

A skill is authored once under `skills/<category>/<name>/`; never maintain a host copy. Run
`node setup-skills.mjs` after checkout. A link setup created earlier — even one now dangling because
a skill's category changed — is migrated to the current canonical path automatically. If it names a
collision, the existing file, directory, or link to a target outside the repository's `skills/` tree
is human-owned and remains untouched. Decide whether to keep or relocate it, then rerun setup.
Existing correct links are accepted. There is no compile step, copy reconciliation, or generated
agent definition. Run `dydo check` to validate the documentation tree.

## Linear boundary mistakes

dydo has no Linear cache, poller, or mirror; its only Linear client is the read-only `dydo map`
([DR 052](../project/decisions/052-dydo-map-read-only-linear-view.md)). Use the official Linear MCP,
UI, API, or integrations for live work. Put only durable Decisions, plans, guides, audits, assimilation briefs,
changelog, pitfalls, and FutureFeature ideas in Git.

## Related

- [Guard System](../understand/guard-system.md)
- [CLI Commands](../reference/dydo-commands.md)
- [Scaffold and Customization](../understand/scaffold-and-customization.md)
