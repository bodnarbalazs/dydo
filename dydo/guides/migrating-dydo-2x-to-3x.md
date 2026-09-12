---
area: guides
type: guide
---

# Migrate from dydo 2.x to 3.x

dydo 3 keeps durable project knowledge in Git and uses Linear for live project management. It no
longer contains a local Notion provider, watchdog, token store, or external-data sync engine.

Keep active work, status, priority, assignment, and dependencies in Linear; keep Decisions, plans,
guides and release evidence in the repository. FutureFeatures live in Linear.

## Migrate a project

The order matters: every `dydo.json` edit below must land before the first dydo 3 command rewrites
the file, because that rewrite drops the old keys unread. `dydo sync`, `dydo template update`,
`dydo fix` and `dydo init <host> --join` each rewrite an existing config as the last thing they do,
and only once everything before it has succeeded: a command that exits nonzero — a template-update
warning, a fix validation error, any other failure — leaves the original bytes exactly as they were,
retired keys included. Every write is atomic, going to a temporary sibling that is flushed to disk
and then renamed over the file, so no failure leaves a half-written config.

1. Upgrade dydo to 3.0.
2. Delete the whole `models` object from `dydo.json`, `models.roles` and `models.agents` alike.
   dydo 3 has no model or effort property: a config `dydo init` creates never contains one, and
   compiled roles are left unbound so that the delegating admiral or Issue Captain chooses the
   model — and the effort where the host exposes one — for each task. A `models` block left in
   place loads without effect and disappears at the first rewrite that succeeds. See
   [Customizing Roles](./customizing-roles.md).
3. Delete the rest of the retired configuration in the same pass, since the first rewrite drops it
   silently: `name`, `paths` (with its `pathSets`), `structure.tasks`, `structure.issues`, `notion`,
   and every nudge's `tools`. A nudge's `audience` key
   survives the rewrite and is still validated, but no longer scopes anything. Removing the `notion`
   object deletes no remote content and no local rollback store; delete those separately, and only
   after confirming that no rollback is needed.
4. Delete stale nudges by hand. `dydo template update` only adds a missing default, matched by exact
   pattern; it removes nothing, and the guard drops a retired block at runtime only while its message
   is still byte-identical to the shipped text. Delete every block whose pattern names a command
   dydo 3 does not have (`dydo dispatch`, `dydo worktree`, `dydo model`), the 2.x blocks that
   guarded `git worktree` and `rm` on a worktree path, any tool-scoped block whose pattern is a
   `{source}` or `{tests}` path-set placeholder (nothing expands it now that `paths` is gone), and
   every `dotnet run` pattern whose command alternation names a retired command — a 2.x config
   lists `roles`, usually beside `task`, `review`, `dispatch` and `watchdog`; an early-3.0 one lists
   `model`. The shipped `dotnet run` pattern arrives beside them.
5. Delete `dydo/_system/templates/`. Nothing reads, updates, or removes it, and `dydo check` reports
   every file in it as missing required frontmatter.
6. Delete `dydo/_system/.local/model-caps/` and `dydo/_system/.local/last-model-cap-restore`, and
   `dydo/_system/roles/` and `dydo/_system/sync-model.json` if either is present. For a retired
   external-sync store such as `dydo/_system/notion_sync_spine/`, either add it to `scanExclude` or
   delete it after confirming that no rollback is needed.
7. Delete `dydo/_system/template-additions/` and `dydo/_system/templates/`; `{{include:...}}` tags are
   retired by Decision 049. Project-specific guidance moves into the skill bodies and project documents
   directly.
8. Delete the `skills` and `frameworkHashes` keys from `dydo.json` and `_system/templates/` from its
   `scanExclude`. A role is now a plain `SKILL.md` folder, not a switchboard entry.
9. Replace the compiled `.claude/skills/`, `.claude/agents/`, `.agents/skills/` and `.codex/agents/`
   trees with the shipped native skill folders (`.claude/skills/<role>/SKILL.md` and
   `.agents/skills/<role>/SKILL.md`), and delete the generated agent definitions. Then run `dydo check`
   and resolve what it reports.

## Live work and host ownership

FutureFeatures are Linear Issues in the FutureFeature status. The human promotes a retained
possibility to contracted work or graduates its intent through `to-project`. Claude Code and Codex continue to own runtime identity,
permissions, isolation, and native coordination.
