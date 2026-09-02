---
area: guides
type: guide
---

# Migrate from dydo 2.x to 3.x

dydo 3 keeps durable project knowledge in Git and uses Linear for live project management. It no
longer contains a local Notion provider, watchdog, token store, or external-data sync engine.

Keep active work, status, priority, assignment, and dependencies in Linear; keep Decisions, plans,
guides, release evidence, and FutureFeatures in the repository.

## Migrate a project

The order matters: every `dydo.json` edit below must land before the first dydo 3 command rewrites
the file, because that rewrite drops the old keys unread.

1. Upgrade dydo to 3.0.
2. Rename `models.roles` to `models.agents` in `dydo.json` before running `dydo template update`,
   `dydo init <host> --join`, or `dydo fix` — the first always rewrites the file and the other two
   rewrite it whenever they change it, and a rewrite keeps only the keys 3.0 names, without a
   warning. Renaming afterwards means recovering the map from version control: the rewrite leaves
   `models.agents` empty and every compiled agent then carries `model: inherit`.
   In the renamed map, delete `planner` and `test-writer` (no such agents) and add
   a tier for `project-planner`, `issue-planner`, `issue-captain`, `research`, and `scout` — nothing
   merges the shipped defaults into an existing config.
3. Delete the rest of the retired configuration in the same pass, since the first rewrite drops it
   silently: `name`, `paths` (with its `pathSets`), `structure.tasks`, `structure.issues`,
   `models.efforts`, `models.fallback`, `notion`, and every nudge's `tools`. A nudge's `audience` key
   survives the rewrite and is still validated, but no longer scopes anything. Removing the `notion`
   object deletes no remote content and no local rollback store; delete those separately, and only
   after confirming that no rollback is needed.
4. Delete stale nudges by hand. `dydo template update` only adds a missing default, matched by exact
   pattern; it removes nothing, and the guard drops a retired block at runtime only while its message
   is still byte-identical to the shipped text. Delete every block whose pattern names a command
   dydo 3 does not have (`dydo dispatch`, `dydo worktree`, `dydo model`), the 2.x blocks that
   guarded `git worktree` and `rm` on a worktree path, any tool-scoped block whose pattern is a
   `{source}` or `{tests}` path-set placeholder (nothing expands it now that `paths` is gone), and
   the two 2.x `dotnet run` patterns whose command alternation still lists `model` — the shipped
   `dotnet run` pattern arrives beside them.
5. Delete `dydo/_system/templates/`. Nothing reads, updates, or removes it, and `dydo check` reports
   every file in it as missing required frontmatter.
6. Delete `dydo/_system/.local/model-caps/` and `dydo/_system/.local/last-model-cap-restore`, and
   `dydo/_system/roles/` and `dydo/_system/sync-model.json` if either is present. For a retired
   external-sync store such as `dydo/_system/notion_sync_spine/`, either add it to `scanExclude` or
   delete it after confirming that no rollback is needed.
7. Delete every `dydo/_system/template-additions/extra-*.md` whose tag no shipped template carries.
   The live tags are `extra-must-reads`, `extra-test-guidance`, `extra-verify`, `extra-review-steps`,
   and `extra-review-checklist`; `grep -rn "{{include:" Templates/` in the dydo repository lists them.
8. Run `dydo template update`. It refreshes the six framework-owned documents under `reference/` and
   `guides/`, prunes every `frameworkHashes` key that does not name one of them, and adds the missing
   default nudges. Never hand-edit `frameworkHashes`.
9. Run `dydo sync`. It compiles the 3.0 skills and agents from the shipped templates and sweeps every
   retired skill's artifacts — `agents/openai.yaml` included — from both hosts. Then run `dydo check`
   and resolve what it reports.

## What remains unchanged

FutureFeatures remain repository-native ideas. A human promotes one to a Linear Initiative, Project,
or Issue when it becomes live work. Claude Code and Codex continue to own runtime identity,
permissions, isolation, and native coordination.
