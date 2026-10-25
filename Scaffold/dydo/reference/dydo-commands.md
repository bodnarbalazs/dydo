---
area: reference
type: reference
---

# CLI Commands Reference

Complete reference for dydo's local setup, documentation, guard, worktree cleanup, validation, testing, and utility
commands.
Live work is managed in Linear through its official surfaces; no dydo command creates, updates,
polls, or mirrors a Linear object; `dydo map` reads Linear on demand and keeps disposable saved maps. FutureFeatures live in Linear and are promoted by the user; historical repository records remain
durable evidence rather than a second work board.

Commands find the project by walking up to the nearest `dydo.json`; `dydo validate` is the exception
and reads it from the current directory. `dydo check`, `dydo graph` and `dydo validate` take
`--root <dir>` to work on another project: `<dir>` is a directory, or a file inside the project (the
walk starts at its folder). They walk up to the nearest `dydo.json`, read
config and docs from there, print `Root: <project root>`, and exit `2` when `<dir>` does not exist or is in no dydo
project. `dydo help` prints the one-screen summary;
`dydo <command> --help` is the authoritative option list.

---

## Setup Commands

### dydo init

Create the project's durable knowledge tree and wire a runtime's guard hook.

```bash
dydo init <integration>              # claude, codex, all, or none
dydo init <integration> --join       # wire this machine, or an added runtime, into an existing project
```

Every mode writes `dydo.json`, scaffolds the `dydo/` folders with their framework documents,
`files-off-limits.md` and `_system/types.json`, writes the `AGENTS.md` entry point (never `CLAUDE.md`), and adds
`dydo/agents/`, `dydo/_system/.local/`, `/.claude/skills/` and `/.agents/skills/` to `.gitignore`. A
documentation or entry-point file that already exists is left as it is. A fresh init records the
running dydo version in `dydo.json` as `dydoVersion`. `none` stops there: no runtime integration.
The runtime modes add, per selected host:

| Host | Files | What they carry |
|---|---|---|
| `claude` | `.claude/settings.local.json` | a `PreToolUse` hook running `dydo guard --host claude` on the matched tools, and the allow entries `Bash(dydo:*)`, `PowerShell(dydo:*)`, `Bash(dydo clean:*)` and `PowerShell(dydo clean:*)` |
| `claude` | `.claude/settings.json` | `env.CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH = "3"` |
| `claude` | `.gitignore` | `.claude/settings.local.json`, `/.claude/skills/` and `/.agents/skills/` |
| `codex` | `.codex/hooks.json` | a `PreToolUse` hook running `dydo guard` on the matched tools |
| `codex` | `.codex/rules/dydo.rules` | an exec-policy `prefix_rule` allowing `dydo clean`, which Codex loads when the project's `.codex/` layer is trusted; dydo rewrites the file on every init and `--join` |
| `codex` | `.codex/config.toml` | `[agents]` with `max_depth = 3` and `max_concurrent_threads_per_session = 16` |
| `codex` | `.gitignore` | `/.claude/skills/` and `/.agents/skills/` |

`all` selects both hosts. Only tool calls the hook's matcher names reach `dydo guard`. When
`.claude/settings.json` or `.codex/config.toml` already holds a conflicting value for a key init
manages, init stops with an error before it writes anything. Init and `--join` also append a
`dydo completions` line to the shell profile they detect, when they detect one.

`--join` targets an already-initialized project: a fresh clone, or a second runtime added later. It
writes the selected hosts' rows above, and the entry point when it is missing, without re-scaffolding
or overwriting the tree, and records the integration in `dydo.json`. It leaves `dydoVersion` alone and
never adds it: a teammate's newer CLI has not adopted that release's skills and documents. `dydo adopt`
records it.

Claude Code 2.1.281 or later reads `AGENTS.md` when no `CLAUDE.md` is in the way. With `claude` or
`all`, init and `--join` therefore remove a `CLAUDE.md` that is dydo's own generated copy (equal to
`AGENTS.md` or the template). A `CLAUDE.md` you wrote is kept, with a warning that it hides
`AGENTS.md`; move its content into `AGENTS.md`, or put `@AGENTS.md` at its top. `CLAUDE.local.md` and `.claude/CLAUDE.md` are never removed, and each gets its own warning:
`CLAUDE.local.md` needs `@AGENTS.md` at its top (or Project instructions set to
`claude-md-and-agents-md`); `.claude/CLAUDE.md` needs `@../AGENTS.md` at its top.

dydo does not compile or install skills, and none ship with the package. A skill is one folder under
`skills/<category>/<name>/`: copy `skills/`, `setup-skills.mjs` and `THIRD-PARTY-NOTICES.md` (the
MIT notices of the adapted skills travel with them) from the dydo repository into the project root,
commit them, and run `node setup-skills.mjs` to create the host discovery links. The script always
creates both `.claude/skills/` and `.agents/skills/`, and init always gitignores both (the
`.gitignore` rows above), in every mode including `none`.

### dydo adopt

Record the dydo release whose skills and documents the project has adopted.

```bash
dydo adopt 3.5.0                     # or v3.5.0; the leading v is dropped
```

Sets `dydoVersion` in `dydo.json`, rewriting the file the way `--join` does. The `update-dydo` skill
runs it once an update is proven. The value is a release like `3.5.0` or `3.5.0-beta.3`. Anything else, a missing
`dydo.json`, or one that does not parse, exits `2` and leaves the file as it was.

---

## Documentation Commands

### dydo check

Validate documentation naming, frontmatter, titles, links, and project-specific rules. It also validates
the off-limits file, legacy FutureFeature shape under `project/future-features/`, retired v2 work records,
uncustomized foundation docs (warning), and `dydo.json` itself; config errors count toward exit `1`.

```bash
dydo check
dydo check <path>
dydo check --root <dir>          # check the project containing <dir>, not the current one
dydo check --root <dir> <path>   # <path>: absolute or cwd-relative (not root-relative), inside that docs tree
```

Exit `0` means no errors. Exit `1` means validation errors were found. Exit `2` means a tool error,
such as a `<path>` outside the docs tree or a `--root` in no dydo project.

### dydo fix

Apply supported documentation repairs.

```bash
dydo fix
dydo fix <path>
```

Repairs include filename normalization, wikilink conversion, and restoration of
required scan exclusions. Review the Git diff afterward.

### dydo index

Regenerate documentation indexes from the configured structure.

```bash
dydo index
dydo index <path>
```

### dydo graph

Show incoming and outgoing documentation links for one file.

```bash
dydo graph <file>
dydo graph <file> --incoming
dydo graph <file> --degree 2
dydo graph <file> --root <dir>
```

### dydo graph stats

Show repository-wide documentation graph statistics.

```bash
dydo graph stats
dydo graph stats --top 20
dydo graph stats --root <dir>
```

---

## Guard Command

### dydo guard

Evaluate one tool call against the two path tiers, the dangerous-command rules, and the project's
nudges. Runtime hooks invoke this command automatically; the argument form is for diagnostics.

```bash
# Hook mode
echo '{"session_id":"manual","tool_name":"Edit","tool_input":{"file_path":"src/file.cs"}}' | dydo guard

# Diagnostic mode
dydo guard --action edit --path src/file.cs
dydo guard --command "git status"
```

`--host claude` marks the Claude Code hook `dydo init` writes; it is not a diagnostic argument, so
the call still reads its payload from stdin. The Codex hook carries no marker.

In hook mode (a PreToolUse payload on stdin, no arguments) exit `0` with empty stdout allows the
action, and exit `0` with one `permissionDecision: "deny"` object on stdout blocks it (the one
`"ask"` is the [mode-set](#dydo-mode) rerun); the reason
carries the `BLOCKED:` text, which still goes to stderr too. A block is not exit `2` because Codex on
Windows runs hooks through PowerShell, which turns exit `2` into exit `1`, and Codex then lets the tool
run. In diagnostic mode (the CLI-argument examples above) exit `0`
allows and exit `2` blocks with `BLOCKED:` on stderr. **Off-limits** paths block
every operation, reads included. **Protected** paths are readable by any tool and writable by none,
Bash included. Both tiers bind on every caller; [Files Off-Limits](../files-off-limits.md) declares
them, their glob syntax, and the whitelist that lifts off-limits patterns. Nudges are configured in
`dydo.json` - see [DynaDocs](./about-dynadocs.md). A nudge matches shell command lines by default; one
with `"match": "new-file"` matches the project-relative path of a file that Write, Edit, NotebookEdit
or a Codex `apply_patch` is about to create, and never fires for a file that already exists or for a
file a shell command creates.

---

## Linear Command

### dydo map

Serve a read-only, local browser map of one Linear Project.

```bash
dydo map                  # print the URL and open it in the default browser
dydo map --no-browser     # print the URL only
```

`dydo map` reads a personal Linear API key from `LINEAR_API_KEY`. Without one it prints how to create
a key and exits `2` before any network call. Otherwise it serves `http://localhost:<free port>/`,
prints that URL, opens it unless `--no-browser` is given, and runs until Ctrl+C. The server answers
GET only; the key stays in the dydo process and never reaches the browser.

Create a personal API key in Linear under Settings > Account > Security & access > Personal API keys.
Replace `<your key>` in the commands below with that key.

On **Windows PowerShell**, set it for this session and save it for your Windows account:

```powershell
$env:LINEAR_API_KEY = '<your key>'
[Environment]::SetEnvironmentVariable('LINEAR_API_KEY', $env:LINEAR_API_KEY, 'User')
```

The first line alone is session-only and is lost when the session ends. The second persists across
restarts for future apps. Fully restart existing terminal/editor apps to pick up the saved value;
the current shell can run `dydo map` immediately because of the first line.

On **bash/zsh**, set it for the current session:

```bash
export LINEAR_API_KEY='<your key>'
```

To persist it across restarts, also add that export line to your shell startup file. For Bash, use
`~/.bashrc` and ensure your login startup file (for example, `~/.bash_profile`) sources it. For Zsh,
use `~/.zshrc`, or `$ZDOTDIR/.zshrc` if `ZDOTDIR` is set. New interactive shells will load it; run
`dydo map` from a configured shell. Running `export` alone does not save it for later sessions.

The page reads `GET /api/teams`, `GET /api/projects?team=<teamId>` and `GET /api/graph?project=<projectId>`.
These routes read Linear afresh. Opening a Project also requests `GET /api/saved?project=<projectId>`:
its response is `{ "snapshot": null }` on a miss, or a snapshot with `graph` and UTC `fetchedAt`.
The viewer captures this baseline before starting its fresh request, so a fast fresh response cannot
overwrite the old snapshot before it is read. A miss or lookup failure also starts fresh fetching
immediately. Fresh fetching overlaps saved layout and the viewing window.
The saved canvas stays visible for at least two seconds after layout and initial viewport fitting,
then fresh changes use the normal Refresh animation. Its timestamp identifies saved data. A failed
fetch retains that map and timestamp, warns, and permits Refresh to retry. An unchanged graph settles
quietly; reduced motion keeps the viewing window without motion.

The server stores only the latest successful map for each opened Project under the OS user's local
application data `dydo/map` directory, outside repositories. Opaque endpoint/key partitions isolate
credentials and key rotation; corrupt or inaccessible files are treated as misses. No Linear writes,
polling, history, or prefetching unopened Projects occur. Delete this directory to forget saved maps.
`DYDO_MAP_CACHE_DIR` overrides the cache root for isolated tests.

 The viewer is embedded when `viewer/dist/` was built before dydo; a dydo built
without it serves a page at `/` saying the viewer was not built, and `dydo map --help` says so too.

`DYDO_LINEAR_ENDPOINT` replaces the Linear GraphQL URL. It is a test seam for pointing dydo map at a
fake Linear, not a user setting.

---

## Mode Command

### dydo mode

Show or set this machine's mode flags.

```bash
dydo mode show                 # list every flag with its value
dydo mode set overnight on     # set one flag
```

The flags live in one file per user and machine, `dydo/mode.json` under the user's local application
data (`%LOCALAPPDATA%` on Windows, `~/.local/share` on Linux), so every project and agent on the machine
reads the same values. One flag ships: `overnight`, `on` or `off`, default `off`. `set` refuses an
unknown flag or value with exit `2` and changes nothing; on success it prints every flag as `show` does.

`dydo mode set` from an agent meets a built-in warn nudge that `dydo.json` cannot remove or weaken. The
first try is blocked with a reminder that only the chief of staff sets the mode. The guard reads the
command as the shell that runs it would, quotes, escapes, here-strings, heredocs and line
continuations included, and splits it only on separators outside quotes. Claude Code's `Bash` tool runs
bash and its `PowerShell` tool PowerShell; Codex, whose payload carries a `turn_id`, runs PowerShell on
Windows and the user's shell elsewhere. A call whose shell is unknown, such as `Bash` from an unmarked
hook on Windows, and any `cmd /c` string are read both as bash and as PowerShell or cmd would. A dydo call at command
position meets the nudge: past environment assignments and wrappers such as `env -i`, `sudo`, `nohup`,
`xargs` and the `&` call operator, as `dydo` under any path or case (`"C:/Program Files/dydo.exe"`,
`DYDO.EXE`), `dotnet ...dydo.dll` or `dotnet run --` with no `--project` or one naming dydo or DynaDocs. The command string of `cmd /c`,
`pwsh -Command`, `bash -c`, `eval` or `Invoke-Expression` and every `$(...)` or backtick substitution
is read with that shell's rules, and `Start-Process dydo -ArgumentList "mode", "set", ...` counts too,
as does an `@(...)` array or `(...)` group as the argument list. A PowerShell `<# ... #>` block comment is skipped. Arguments
of any other program are text, so a commit message, a PR body or a here-string that mentions
`dydo mode set`, even after a `;` inside the quotes, does not meet it. A command the guard cannot read,
such as one with an unbalanced quote or one piping text into a shell, meets it whenever it contains
dydo, mode and set in that order. On the rerun the guard answers `permissionDecision: "ask"` only when the hook command carries
the `--host claude` marker `dydo init` writes into the Claude Code hook and the permission mode is
`default`, `acceptEdits`, `plan` or `auto`, so the user approves or rejects it. The environment is not
the signal: `CLAUDECODE=1` is inherited by a Codex session started from a Claude Code shell. Everywhere
else, Codex, `bypassPermissions`, `dontAsk`, an unknown host or a Claude Code hook written before the
marker existed, it denies, and the user runs the command in their own terminal, where no guard runs.
To get the prompt on an older install, run `dydo init claude --join` to rewrite the Claude Code hook.
The guard stops a stray agent setting the mode by accident, not a deliberate bypass such as an agent
forging the hook marker or writing the mode file directly.

---

## Worktree Command

### dydo clean

Remove this repository's linked worktrees without losing work.

```bash
dydo clean --dry-run <worktree>...   # what would happen to the named worktrees; changes nothing
dydo clean <worktree>...             # remove the named worktrees, by path or branch name
dydo clean --all                     # remove every linked worktree it may remove
dydo clean --archive                 # list the archive of uncommitted work
```

Name the worktrees, by path or branch, and run from outside them; naming none, or naming some with
`--all`, is a usage error with exit `2`. Run `dydo clean --dry-run <worktree>` first, look at what would
be archived, and commit anything worth keeping, then `dydo clean <worktree>`. `--all` also reaches other
sessions' worktrees, so confirm no session is live before using it. Each worktree
gets one line: removed, archived and removed (with the archive entry), skipped (with the reason) or
failed (with the reason), followed by whether its local branch was deleted or kept; a skipped or
failed worktree always keeps its branch, and its line says so.

- **Never touched:** the main checkout, the worktree holding the current directory, a locked worktree,
  one a running process is using, and one under T3's worktree root `~/.t3/worktrees` (archive the T3
  thread instead). Each is skipped with its reason. So is a worktree whose `dydo.json`, at HEAD or
  in the tree, cannot be parsed, dry run included: which evidence it holds is unknown.
- **Submodules:** the archive reads only the worktree itself, so a worktree with an initialized
  submodule that has staged, unstaged or untracked changes, or a commit on any local ref (HEAD, a
  branch, a tag, the stash) that none of its remote refs reach, is skipped with a reason naming the
  submodule: commit and push it, or clean it, first. A worktree whose
  submodules are clean and pushed is removed as usual.
- **Uncommitted work is archived, then removed.** Staged, unstaged and untracked files git does not
  ignore, plus dydo's ignored evidence under `<root>/agents/` for every dydo root (the one
  `dydo.json` names at HEAD, the one the worktree's `dydo.json` names, and `dydo`), are copied to
  `<git common dir>/dydo/clean-archive/<UTC timestamp>-<branch>/`: `tracked.patch` (`git diff HEAD
  --binary`), `untracked/` and `evidence/` with their paths, and `manifest.json` (worktree, branch,
  HEAD, files, sizes, time, links). A link among those paths is never followed or copied: its
  `links` record keeps its path, whether it links a directory or a file, and its target. Other ignored
  files (`bin`, `obj`, `node_modules`, skill links) are regenerable and dropped, and so is build output
  under `<root>/agents/`: a file below a `bin`, `obj`, `node_modules` or `.vs` folder there, in any case,
  is not evidence, and the worktree's line counts the files skipped. A `WARNING:` on stderr lists every
  archived file, the count of build output skipped, and the archive path; there is no confirmation step. If the archive cannot be
  written, that worktree's line is `failed` and nothing is removed: no unlinking, no removal, no
  prune, and the branch is kept. When `<git common dir>/dydo` or `clean-archive` is a link, dydo does
  not follow it: no work is archived (so those worktrees fail), nothing is pruned or listed, the run
  prints `archive not pruned` with the reason and exits `2`.
- **Restore:** in a checkout of the entry's HEAD, `git apply <entry>/tracked.patch`, copy
  `untracked/` and `evidence/` back, and recreate each link in `manifest.json`'s `links` at its path,
  pointing to its target.
- **Retention:** every run except `--dry-run` and `--archive` prunes entries older than 90 days, then
  the oldest entries until the archive fits `clean.archiveSizeCapMb` in `dydo.json` (default 1024).
  Entries written by the same run are never pruned.
- **Branches:** after `git fetch --prune origin`, the local branch is deleted when its tip is
  reachable from `origin/<default>`, or when no changes are left to merge into it, as after a squash or
  rebase merge: `git merge-tree --write-tree origin/<default> <tip>` merges cleanly to exactly
  `origin/<default>`'s tree, so the branch holds nothing origin lacks. That merge runs in a scratch bare
  repository that borrows the real object store, in its object format (SHA-1 or SHA-256), and reads no repository, global or system config, no
  inherited `GIT_*` variable and no attributes, so no merge driver, builtin or configured, can resolve a change away, and even a dry run adds nothing
  to the repository. Otherwise the branch is kept and the line says why (not merged,
  no origin, fetch failed, or the squash check failed, as with a git older than 2.38). The default is the branch origin advertises now (`git ls-remote --symref origin HEAD`),
  so a stale local `origin/HEAD` never decides; only when origin cannot answer do the local
  `origin/HEAD`, then `origin/main` or `origin/master`, stand in. `git branch -d` runs first; it also
  wants the branch merged into the current checkout's HEAD, so when it refuses,
  `git update-ref -d refs/heads/<branch> <verified tip>` follows, which deletes the branch only while it
  still points at the tip found on origin: a branch that moved since is kept. Remote branches are never
  touched.
- **Removal:** every junction and symlink inside the tree is unlinked first, so nothing recursive can
  follow one out of the tree. Then `git worktree remove --force` (the archive already holds the dirty
  state); when that fails, as with paths past 260 characters on Windows, dydo deletes the folder
  itself through the extended-length path and runs `git worktree prune`.
- **Busy worktrees:** on Windows a worktree counts as in use when its folder cannot be renamed (a
  process has its working directory or an open file inside); on Linux when a process's working
  directory is inside it. macOS has no detection, so stop processes there first. If the Windows
  rename cannot be moved back, even after retries, that worktree's line is `failed`, naming where the
  folder is and the `Move-Item` that puts it back; dydo locks the worktree so `git worktree prune`
  keeps it meanwhile (unlock it after the move), and the run goes on.
- **`--dry-run`** prints the same lines and the same archive warning with `would`, and changes
  nothing: no fetch (branches are judged against the last fetch), no archive, no removal, no prune,
  no index rewrite (dydo runs git with optional locks off, so reads do not refresh a stat cache),
  and no busy check, since the Windows check renames the folder; each line says the busy check was not
  run.

Exit `0` when nothing failed; exit `2` when a removal failed, or when a named worktree was not
removed (unknown, main checkout, current directory, locked, submodule work, busy or under T3). With `--all`, skips
exit `0`.

---

## Validation Command

### dydo validate

Validate `dydo.json` deserialization and nudge definitions: each nudge's pattern, message,
severity, audience, and `match` (absent, `command`, or `new-file`).

```bash
dydo validate
dydo validate --root <dir>
```

This validates dydo's local configuration. It does not validate or provision Linear.

---

## Testing Command

### dydo gap-check

Run the project-configured coverage gap check.

```bash
dydo gap-check
dydo gap-check --force-run
```

The nearest `dydo.json` must contain `testing.runner`: a nonempty string array whose first item is
the executable and whose remaining items are fixed arguments. `dydo gap-check` starts it directly in
the configuration directory and appends every caller argument exactly as supplied. `--force-run`
above is such an argument: dydo's example runner defines it, and dydo itself has no such option. Its exit code is the
runner's exit code; configuration, startup, and cancellation failures exit `2`.

---

## Utility Commands

### dydo completions

Generate a completion script for the requested shell.

```bash
dydo completions bash
dydo completions zsh
dydo completions powershell
```

### dydo version

```bash
dydo version
```

### dydo help

```bash
dydo help
```


## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success or action allowed |
| `1` | Validation errors or command failure |
| `2` | Tool error or guard block |

## Related

- [DynaDocs](./about-dynadocs.md) - Product boundary and operating model
- [Files Off-Limits](../files-off-limits.md) - The two path tiers `dydo guard` enforces
- [Writing Documentation](./writing-docs.md) - Documentation conventions validated by dydo
