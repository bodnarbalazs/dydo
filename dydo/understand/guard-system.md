---
area: understand
type: concept
---

# Guard System

How dydo enforces boundaries through the PreToolUse hook. Every tool call the hook's matcher names - reads, writes, searches, bash, in the main thread and inside every subagent and workflow - passes through `dydo guard` before execution. Three layers: path tiers (off-limits and protected), dangerous-bash detection, and nudges.

---

## How the Hook Intercepts Tool Calls

The guard integrates with the platform through the **PreToolUse** hook event. Before each matched tool call, the platform pipes a JSON payload to `dydo guard` via stdin:

```json
{
  "session_id": "abc123",
  "tool_name": "write",
  "tool_input": {
    "file_path": "src/foo.cs",
    "content": "..."
  },
  "hook_event_name": "PreToolUse"
}
```

The guard evaluates the request and returns:

- **Exit 0, empty stdout** - action allowed (a `NOTICE:` or `WARNING:` on stderr may ride along)
- **Exit 0, one deny decision on stdout** - action blocked. Stdout holds exactly one object,
  `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"<text>"}}`,
  where the reason is everything the call wrote to stderr: the `BLOCKED: <reason>` line, its
  follow-up lines and any `WARNING:` lines. The same text still goes to stderr. Non-ASCII is
  escaped, so a shell that re-encodes stdout cannot break the object.

A block decides through stdout rather than exit 2 because Codex runs a hook through its session
shell, `powershell -NoProfile -Command "dydo guard"` on Windows, and Windows PowerShell and pwsh
both turn a native exit 2 into exit 1 there; Codex treats exit 1 as a failed hook and runs the
tool. Codex and Claude Code both honour the deny decision at exit 0 (LC-1021). A payload naming
another `hook_event_name` keeps the old exit 2 with stderr only, and so does the CLI-argument mode.

`dydo init` wires the hook into a committed file, so a fresh clone is guarded: `.claude/settings.json` on Claude, `.codex/hooks.json` on Codex ([DR 056](../project/decisions/056-claude-guard-in-committed-settings.md)). A teammate without a dydo CLI gets a non-blocking hook error on each matched call, and nothing is guarded for them. Init installs the matcher that decides which calls arrive: on Claude `Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit`, on Codex `Bash|apply_patch|Edit|Write|Agent|shell_command|exec|local_shell|unified_exec`. A tool outside its host's matcher never reaches the guard.

There is no identity, no staging, no per-role permission matrix: the path tiers, dangerous-command rules and command nudges apply to every caller, every time ([Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)). One caller *kind* is distinguished, and it is not a role: a call carrying `agent_id` is a sub-agent, which may run only the `dydo` gate and read-only commands its work needs: `check`, `fix`, `gap-check`, `graph`, `validate`, `mode`, `clean` (a Ticket Captain removes its own worktree on close-out), `pr-ready` (it checks its tree before opening a PR), `merge` (a Merge Sub-ticket's code-writer integrates a reviewed PR into its feature branch), `help`, `version` and `-h`/`--help`/`--version`. Setup, generation and hook commands (`init`, `adopt`, `index`, `guard`, `map`, `completions`, `_complete`) stay with the top-level session. A command the 3.3.0 pattern does not match (`dydo ` at the start or after `;`, `&` or `|`) is allowed exactly as in 3.3.0, so `find dydo -name '*.md'`, a continued `git diff \` line ending in `dydo src`, and `echo dydo sync` pass. A matched command is allowed only when, after line continuations are joined and quotes and escapes removed, every `dydo` token anywhere in it that is followed by a word is followed by an allowed one, so `dydo check && dydo init` and `dydo check $(dydo init)` are blocked, and so is `dydo check dydo src`, where the directory `dydo` reads as a call.

---

## Layer 1: Path Tiers

`dydo/files-off-limits.md` declares two tiers, both applying to every caller.

**Off-limits** patterns hard-block **every** operation - read, write, search, or bash.

- **Covered:** secrets and credentials (`.env*`, `*.pem`, `*.key`, `secrets.json`, database configs) plus the hardcoded `dydo/_system/**` machine state.
- **Whitelist:** a `## Whitelist` section carves exceptions (e.g. `.env.example`) to the configured patterns - never to the hardcoded ones.
- **Patterns** use glob syntax: `**/` for optional directory prefix, `**` for any path, `*` within a segment, `?` for a single character; matching is case-insensitive on every platform.
- Host memory folders outside the repo (Claude's `projects/*/memory/` and `agent-memory*/`, Codex's `memories/`) are readable past these patterns, but every agent write to them is blocked ([DR 055](../project/decisions/055-native-agent-memory-off-and-guarded.md)): a lesson goes into a skill, a Decision Record or a doc.

```
BLOCKED: Path is off-limits to all agents.
  Path: .env
  Pattern: .env*
  Configure exceptions in dydo/files-off-limits.md
```

**Protected** patterns (a `## Protected Patterns` section) invert the emphasis: **every agent may read them, none may write or delete them** ([Decision 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) §10). Membership is dydo's own system files - `dydo/index.md`, `dydo/files-off-limits.md` and the hardcoded `dydo.json` - because agents read them to orient themselves, `dydo/index.md` on every entry prompt's order. The tier's contract is that no agent writes them *directly*, and the user owns their content; dydo's own commands still rewrite what they manage - `dydo index` regenerates `dydo/index.md`, and `dydo fix` writes `dydo.json`. Those arrive as `dydo` command lines with no file path to extract, so they pass: the tier stops hand edits, not dydo's tooling. `CLAUDE.md`, `AGENTS.md` and harness config files stay outside the guard entirely: the harness defends its own files, and off-limits keeps its original meaning of files agents must not even read.

The tier binds on the mutating call only: the `Edit`, `Write` and `NotebookEdit` tools, Codex's `apply_patch`, a CLI `--action` of `edit`, `write` or `delete`, and every operation the bash analyzer extracts from a shell command that is not a read of that path - write, delete, move, copy, permission change. `Read`, `cat`, `head` and search pass. The whitelist does not apply.

A shell command is checked by the file operations its text names. Two kinds of code whose file operations that text does not show are refused outright, whatever they touch: inline interpreter code (`python -c`, `node -e`, `ruby -e`, `perl -e`, `php -r`) and a Python program read from stdin (`python -` or `py -`, or `python` given no script while a heredoc, here-string, pipe or `<` redirection feeds its stdin). That Python is the command's program, or the program a runner's `run` subcommand starts, by one shape for any runner: the runner's first operand is `run` and Python is the first operand after it, options allowed before each, and a word after an option may be its value. So `uv run python - <<'EOF'`, `poetry run python3 -`, `uv --directory tools run python -` and `printf ... | uv run --with requests python` are refused, while `uv run python script.py`, `uv run python -m pytest`, `poetry run pytest` and `uv pip install ...` pass. Python behind a runner with no `run` subcommand (`uvx python -`, `pyenv exec python -`) or behind a second runner that `run` starts (`uv run poetry run python -`) is not recognised. The line is read as its shell reads it: a `<` or `|` in a comment or quoted text feeds nothing, and a pipe feeds the command it leads into also as the first command of a `( )` or `{ }` group and inside a `$(...)` or backtick substitution, double-quoted or not, so `printf ... | (python3)` and `echo "$(printf ... | python3)"` are refused. Nothing is read from stdin when nothing feeds it: a bare `python` prompts, `command -v python3` only looks it up, and `xargs python3` gets its script from xargs. Any other interpreter reading a program from stdin (`node - <<'EOF'`, `cat x.js | node`, `ruby` or `perl` fed a program) is not given Python's blanket refusal, and its program is not followed, like a script file; the ordinary raw-text and path checks still apply to its command line, a heredoc body it reads included, as shell text. So `perl <<'EOF'` whose body holds `open F, '>', 'notes.md'`, read as a redirection, is still refused as an unverifiable write, while a `node - <<'EOF'` body calling `writeFileSync` passes with a warning. A script file runs, and its own file operations are not followed: a `python tools/x.py` that writes a protected file is not stopped. The tier stops accidents, not a deliberate route around it.

```
BLOCKED: Path is protected - every agent may read it, none may write or delete it.
  Path: dydo/index.md
  Pattern: dydo/index.md
  Detected: Write via sed -i
  This file is human-owned: read it freely, and ask the user for any change.
```

---

## Layer 2: Bash Command Analysis

Bash commands get deeper treatment than direct tool calls:

1. **Dangerous pattern detection** (immediate block): recursive root/home deletes, fork bombs, direct disk writes (`dd`), download-and-execute (`curl | sh`), base64 decoded into an interpreter, eval of variable content, history clearing, security disables (SELinux, firewall), shadow/passwd access, inline interpreter execution (`python -c`) and a Python program on stdin (`python -` or `py -`, or `python` given no script while a heredoc, here-string, pipe or `<` redirection feeds it; read at command position or as the program a runner's `run` subcommand starts, as `uv run python -`, so a quoted mention, `ps aux | grep python`, `uvx python -`, a feed in a comment (`python # < input`), a bare `python`, `command -v python3` and `xargs python3` pass), which would hide file operations from the analysis below. The stdin refusal says to change files with the file tools or a script file, and that a change to a protected file goes to the user. The built-in list also blocks destructive Git pushes and recovery removal, destructive GitHub CLI commands, disk formatting, privileged deletes, system-wide permission changes, and access to password managers or secret-key exports. Raw worktree removal is blocked too: `git worktree remove` with a force flag (`-f`, `-ff`, `--force`), also after a quoted `git -C "C:\My Repo"`, and a recursive delete (`rm -r`, `Remove-Item -Recurse`, `rd /s`) of a worktree itself, meaning a `worktrees` folder or one entry in it (two deep under T3's `.t3/worktrees/<project>/<thread>`); the path may be bare or quoted, and a quoted entry name may hold spaces; deleting inside a tree, such as its `bin` or `obj`, stays allowed. The refusal says to run, from outside the tree, `dydo clean --dry-run <worktree>`, then `dydo clean <worktree>`, which archives uncommitted work and never follows a link. Plain `git worktree remove` stays allowed because git refuses a dirty tree, and a worktree outside a `worktrees` folder is not recognised: the rule stops accidents, not a determined agent. These rules cannot be configured away.
2. **Nudges**: Layer 3 is evaluated here, before any path is extracted.
3. **Chained `cd` block**: `cd /path && command` breaks path analysis - run `cd` separately or use absolute paths. Skipped for dydo's own commands.
4. **Bypass detection**: command substitution (`$(...)`), base64/hex decode, variable expansion, embedded newlines - flagged because they can obscure the paths actually being touched. On their own each is a `WARNING:` on stderr and the command proceeds; combined with any extracted write, delete, move, copy or permission change the command is **blocked outright**, because the path it would touch cannot be verified. So `cat $FILE` warns and runs, while `echo x > $OUT` is denied. Literal data is not searched: the body of a quoted-delimiter heredoc (`<<'EOF'`, `<<"EOF"`) or a PowerShell `@'...'@` here-string that only data commands read, as below, is neither expanded nor run, so a `cat > notes.md <<'EOF'` body holding `$5` or backticks writes its literal destination. An expanding body (`<<EOF`, `@"..."@`) is still searched, apostrophes and all, so `Don't $(rm dydo/index.md) it's` in a `<<EOF` body is refused. Quoted text on a command line, single-quoted or double-quoted, is searched as before: ``sed -i 's/`x`/y/' f``, `printf '%s' '...$HOME...' > notes.md` and `echo don\'t $(rm dydo/index.md) won\'t` are refused. A heredoc or here-string body is data only when data commands alone read it: the command it feeds is `cat`, `tee`, `grep`, `echo`, `Write-Output`, `Set-Content`, `Add-Content` or `Out-File`, named as such (a here-string may also stand alone before a pipe), and every command its output is piped on to is one of them too. Its line must be the command's first line of code and read for certain, by every shell that may run it, as simple commands joined by `;`, `&&`, `||` or pipes, with no group, substitution, compound command or line continuation, and no pipe carried onto the next line. Any other consumer, under any path or quoting, keeps the body as code exactly as before (`bash <<'EOF'`, `"C:/Program Files/Git/bin/bash.exe" <<'EOF'`, `. /dev/stdin <<'EOF'`, `cat <<'EOF' | sudo bash`, `@'...'@ | wsl bash`, `iex @'...'@`, `{ cat <<'EOF'; } | bash`, `cat <<'EOF' |` with `bash` after the terminator), so its lines are checked like any other. A here-string in a command bash may run (any but a PowerShell tool call) must also read to bash as words of that command, since bash takes its apostrophes as quotes. A runner named in a comment or an argument (`cat > helper.sh <<'EOF' # bash script`, `grep -c bash <<'EOF'`) feeds nothing, and a command before or after the body's command on its line is its own (`mkdir -p docs && cat > docs/a.md <<'EOF'`, `cat > run.sh <<'EOF' && bash run.sh`). A delimiter is letters, digits, `_` and `-`, quoted or not (`<<'END-OF-FILE'`). The text after the terminator is read from the original command on its own, so no quote or comment of the opener line (`# don't expand`) reaches into it. A line break counts only where it separates commands: one in a double-quoted word, such as a multi-line commit message, or in a body only data commands read, the break ending a heredoc's opener or delimiter line, and a blank or comment-only line, such as the trailing line breaks after a terminator, do not. One in single-quoted text counts, as before. The destination and every command after the terminator are still checked.
5. **File operation extraction**: the command is tokenized into reads (`cat`, `grep`), writes (`tee`, `>`, `>>`, `sed -i`), deletes (`rm`), copies/moves (`cp`, `mv`), and permission changes (`chmod`) - and each extracted path is checked against off-limits - and, for anything that is not a read of it, the protected tier - individually. A chain can't smuggle a guarded path past the guard. A heredoc or here-string body a command only reads is no command, so a body line such as `rm notes.md` extracts nothing. A command whose name is a variable (`$CMD file`) is unknown, so its path arguments count as uncertain writes. The one exception is a PowerShell assignment from a content reader, `$p = Get-Content notes.md` (spaced or not; also `gc`, `cat`, `type` and `Select-String`), which is a read of that path; any other assignment stays an unknown command, and there is no PowerShell evaluation.

For command-nudge body relief, each expansion retains its own completed token, child execution and source origin. The parent is checked at the expansion's insertion site, with child code kept separate from parent quoting. Closing and reopening a Bash quote or appending an invalid PowerShell here-string suffix cannot acquire relief from a harmless inner body. Every selected representation must be accounted for, including normalized words, stdin, extracted scripts and captures; an unknown consumer or host remains unknown through recursive analysis. These checks do not change the separate file-operation and embedded-newline diagnostics above.

The guard fires on `dydo` commands themselves too - dangerous patterns, nudges and the path checks apply to dydo's own CLI like anything else; only the `cd` coaching is skipped for them.

### David Ondrej denylist audit

DYD-188 compared [the upstream denylist](https://github.com/davidondrej/skills/blob/main/hooks/dangerous-patterns.txt) (blob `02e930aaf9879b879475baff5142f78e2df52f2a`) and its [test matrix](https://github.com/davidondrej/skills/blob/main/hooks/test-guard.sh) with the built-in analyzer. The following table records the disposition of each family. Command-shaped detections use upstream's anchor: the command name, optionally path-qualified (`/usr/bin/dd`), starts the line or follows whitespace or a shell separator (`;`, `&`, `|`, `(`, `)`, `{`, `}`, a backtick, `!`, `\`). So `sudo`, `nice`, `xargs`, `ssh host`, assignments such as `LC_ALL=C` and keywords such as `then` need no wrapper list. A quote starts a command only after a shell `-c` flag or an `ssh` remote command, so `echo 'dd ... of=/dev/loop0'` is allowed. `dd` writes to `sd*`/`nvme*`/`vd*`/`mmcblk*` targets still block anywhere in the command text, as before. A command named later inside quoted prose follows whitespace and is blocked, as upstream does: `echo 'sudo dd ... of=/dev/sda'` fails closed. The password-store rule (`pass` or `gopass` with a subcommand, a flag or one name) is the exception: its shapes are read on the text, then a `pass` or `gopass` must run at command position. A heredoc body, a Python `pass` statement, a quoted `grep PASS` pattern or a PR body saying "pass" is no access, while `pass show x`, `gopass show x` and `bash -c "echo; pass show x"` are. A line that cannot be read for certain (an unbalanced quote, a pipe into a shell) keeps the text's verdict. Every rule has a 250 ms match timeout; a command that exceeds it is blocked as too complex to verify. Option values and path targets limit false positives.

| Family | Disposition | Boundary and benign example |
|---|---|---|
| Root/home delete, fork bomb, shell download-and-execute | Already covered; extended `rm` for `/Users`, `--no-preserve-root` and `curl \| sudo zsh` | `rm -rf /tmp/build-cache`, `curl ... \| jq` allowed |
| Disk write/format | Extended `dd` writes to `/dev/*` except `/dev/null`, at any command boundary above; added `mkfs*` and destructive `diskutil` verbs | `dd ... of=backup.img`, `dd ... of=/dev/null` allowed |
| Privileged delete | Added `sudo` with optional flags, flag values or `--` followed by `rm` | `sudo brew services restart` allowed |
| Remote Git rewrite/delete | Added `git push -f`, `--force`, `--delete`, `-d`, `+ref`, `:ref` | `--force-with-lease`, normal push, dry run allowed |
| Git recovery removal | Added reflog expiry `now` and gc prune `now`/`all` | dated expiry and prune allowed |
| System permissions | Added `chmod 777 /` and `chown -R ... /` | changes to a named file or directory allowed |
| GitHub CLI mutation and tokens | Added repository/release/secret/key delete, API DELETE, public visibility and `gh auth token` | repo view, API GET/POST, auth status allowed |
| Credential stores | Added named password CLIs, sensitive `op` subcommands, macOS keychain password verbs, GPG/GPG2 secret export, `.password-store` paths and named password `.app` bundles at any path, plus app launch/removal | `op --version`, certificate lookup, public GPG export, unrelated apps and `brew`/`open` allowed |
| Generic `git reset` and `git clean` | Excluded: absent from upstream and local, recoverable operations outside this ticket's contract | Existing path guard still evaluates extracted operations |

The upstream POSIX expressions are translated to .NET regexes rather than copied verbatim. The hook evaluates these before nudges or path analysis, so a matching command blocks for every caller even when it names no local file.

---

## Layer 3: Nudges

Nudges are project-configurable rules in `dydo.json`: a pattern plus a message, at one of three severities.

| Severity | Behavior |
|----------|----------|
| `notice` | `NOTICE:` on stderr, never blocks (exit 0) |
| `warn` | Blocks once with "(Run the same command again to proceed anyway.)"; the retry passes. The pass-through marker lives in `dydo/_system/.local/` (gitignored). A command warn's is keyed by the nudge's pattern hash and the command with whitespace runs collapsed, so only the same command, retried, passes; a different command matching the same nudge is warned on its own. Global, with no agent or session in the key ([Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)). A marker has no expiry and stays until its retry spends it. |
| `block` | Always blocks |

- **Command nudges** - regex matched against shell command text. Capture groups substitute into the message (`$1`, `$2`, …) from the first raw match that counts. Every unquoted match counts, including arguments of `rg`, `echo` and unfamiliar wrappers. Unknown quoted operands count too: `script -c`, `su -c`, `tmux` and unfamiliar command-string consumers cannot silence a warning by quoting it. Quoted-data relief is limited to four recorded forms: the single-quoted positional pattern after `rg -n` (with following `--glob` values and paths), the single-quoted positional pattern after `git grep -n -E` or `git grep -n -i -E`, the double-quoted positional pattern after `grep -iE`, and the quoted `-match` right operand in the recorded `Get-CimInstance Win32_Process | Where-Object` predicates on `$_.CommandLine` or `([string]$_.CommandLine)`. Other operands, option forms and quoted text do not inherit that relief. Interpolation, concatenation and malformed syntax earn no exemption. Comments and literal data bodies keep their scoped relief; executable or unknown script bodies count. Known shell/eval/remote command strings are read recursively, so an inner recognized data operand can be exempt while other code in the same string still counts. Quoted executable paths and substitutions retain their warnings. In `rg -n 'text; npm install example' docs; npm install real`, the capture belongs to the real launch. A line that cannot be read for certain matches as its text does.

Body relief follows the complete output chain, including the consumer of an enclosing shell. A literal body printed by `cat` stays counted when that output reaches a shell or an unknown consumer, even through a nested shell's stdin or command string. Ending an inner pipeline with a sequence does not erase that enclosing consumer. Direct harmless nested bodies retain relief. Each body and executable expansion retains its own source ranges and host; identical text in another body cannot lend it an exemption. A PowerShell-shaped here-string in Bash must also place the particular launch inside Bash's quoted text. Unsupported flow or uncertain occurrence transport counts. This bounded analysis does not resolve aliases, follow script files or evaluate file-descriptor routing.

Two shipped nudges carry policy. The **review-block nudge**: a `gh pr create` whose command text has no `Independent review` in it is warned once, because nothing reaches the user that an independent agent has not reviewed and the PR body is where that proof lands ([Decision 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) §3). At `warn` an honest exception costs one retry; raising it to `block` is the user's edit to `dydo.json`, made only if the discipline erodes.

The **install nudge** warns on installing or adding a library or tool: `pnpm`/`npm`/`yarn`/`bun` `add`, `dlx`, `create`, `update`, `upgrade`, `link` and `install <package>`, `npx -y`, `uv add`/`pip`/`tool`, `uvx`, `pipx`, `pip install`, `dotnet add`/`tool install`/`workload install`/`new install`, `apt`, `snap`, `flatpak`, `cargo`, `go`, `gem` and `brew install`, optionally behind `sudo`. Installing needs the user's permission: explicit in the conversation, or implicit in the approved plan or ticket that names the install. The agent retries the same command only then; otherwise it stops and asks. Restoring locked dependencies (`pnpm install --frozen-lockfile`, `npm ci`, `uv sync --frozen`, `dotnet restore`, a bare `pnpm install` or `npm install`) never triggers it. It is best-effort: a `time`, `nohup` or `xargs` prefix, `bash -c '...'` and `yarn global add` slip past it, and a heredoc body quoting an install can trigger it. `dydo init` ships it to new projects and `dydo adopt` adds it once to existing ones at 3.6.0, skipping a project whose `dydo.json` already holds the same pattern.

**Shipped defaults and self-healing:** the indirect-dydo-invocation nudges (`npx dydo`, `dotnet dydo`, `python dydo`, …) are severity-pinned - `MergeSystemNudges` reconciles config against the shipped set on every nudge evaluation, in memory and without rewriting `dydo.json`: a deleted block-default is re-added, a downgraded severity is restored to `block`, and a nudge still carrying a known-stale shipped message is healed to the current text or dropped if its default was retired. A message the user customized matches no known-stale text and is never clobbered.

---

## Also Enforced

- **Plan-mode block**: `EnterPlanMode`/`ExitPlanMode` are blocked - planning happens through the Wayfinder or Code Writer skill and plan records, not the platform's plan mode.
- **PR-ready check**: a PR shows the branch as the remote holds it, so a PR-opening gh call is blocked while the tree it shows has staged, unstaged or untracked files, or HEAD has no upstream or differs from it (the local remote-tracking ref; no fetch). The calls are `gh pr create`, `gh pr new`, `gh pr ready`, a `gh api` POST to `repos/OWNER/REPO/pulls`, and `gh api graphql` naming `createPullRequest` or `markPullRequestReadyForReview`; a REST PATCH of `draft` cannot ready a PR and is not one. The line is read as its shell reads it: PowerShell for the PowerShell tool and on Codex for Windows, bash for Claude Code's Bash tool, both ways when unknown, and also as PowerShell when a bash line uses PowerShell syntax (a backtick line continuation, `&` starting a line, `$env:`); a reading that loses a `--head` another reading names adds no call of its own. A call counts at command position: after `;`, `&&`, `||`, `|`, a line break, a group or a subshell, past `VAR=value` words, shell keywords and `env`, `command`, `builtin`, `exec`, `nohup`, `time`, `nice`, `timeout`, `sudo`, `doas`, `xargs`, `stdbuf`, `ionice`, `setsid`, `unbuffer`, `caffeinate` and `wsl` (the same reading the dydo-command rules use), with gh under any path, case or `.exe` and gh's `-R`/`--repo` before `pr`. A commit message, a `grep` pattern, a comment or a heredoc body quoting one does not count, but a command substitution runs its command, so a `$(...)`, or in bash a backtick substitution, anywhere its shell expands it (inside double quotes too, not single) is read like a direct call. The tree is the call's directory: the tool input's `cwd`, else Codex's `workdir` (a relative one under the payload's `cwd`), else the payload's `cwd`, else the project root, as when Codex sends none, and the refusal says which. A call naming its branch (`--head [OWNER:]BRANCH`, `gh pr ready BRANCH`, a REST `head` field) is checked in every worktree with that branch checked out instead; it is refused when none has it, or when git lists one it cannot open (a stale, prunable entry). The refusal names every tree it checked, the clean ones marked clean. A call acting on a repository other than the tree's `origin`, by host, owner and name (case and `.git` aside; github.com when no host is named), is refused: run it from that repo's checkout. That repository is the one gh picks: a `gh pr ready` PR URL, else the last `-R`/`--repo`, else an inline `GH_REPO`, else an inherited `GH_REPO`, with `env -u`/`--unset GH_REPO`, `env -i` and an empty `GH_REPO=` applied left to right; for `gh api`, the endpoint's (its URL host or `--hostname`), where a lone `{owner}` or `{repo}` takes its part from `GH_REPO` or the checkout's origin and the literal part is compared. A directory outside git is allowed, wrapped call or not. It stops an accident, not a bypass, so it decodes nothing: a `pwsh`/`powershell -Command`, `bash`/`sh`/`zsh`/`dash`/`ksh -c`, `cmd /c` or `/k`, `eval`, `iex`/`Invoke-Expression`, `env -S`/`--split-string`, `ssh` (its remote command) or `watch` whose command string, read the same way, makes such a call is refused unread, asking to run gh directly so the check can see it, and so is any `-EncodedCommand`. Aliases, functions and other programs are not followed. A Codex argv-array command in its shell envelope (`[bash|sh|zsh, -c|-lc, script]` or `[pwsh|powershell, options, -Command, script]`) is its script, checked as a direct call; any other array is refused when its program is a wrapper holding such a call or any of its words reads as one. A help request is no PR: `gh pr create`, `gh pr new` or `gh pr ready` given `--help` or `-h` as a flag of its own prints gh's help and acts on nothing (gh 2.88.1), while `--help` as a flag's value (`--title --help`) or after `--`, or a real call later in the line, still counts. The refusal names what failed and asks for [`dydo pr-ready`](../reference/dydo-commands.md#dydo-pr-ready) there, whose `--body` also checks the body names its ticket and holds the reviewer's `PASS` block.
- **Merge-route blocks** ([DR 061](../project/decisions/061-agents-merge-only-into-feature-branches.md) decision 7): agents merge only through [`dydo merge`](../reference/dydo-commands.md#dydo-merge), into a `feature/*` branch, and the default branch is the user's. Every caller, top-level or sub-agent, is refused these, under Claude's Bash and PowerShell tools and Codex's argv arrays:
  - `gh pr merge`, including `--auto`, `-R`/`--repo` before `pr`, and gh under any path, case or `.exe`. It is refused into any base, because the guard cannot read a PR's base offline.
  - `gh api` on `pulls/N/merge`, `pulls/N/merge-async` or `merges`, as a path or a URL, with any method.
  - `gh api graphql` with a field naming `mergePullRequest`, `enablePullRequestAutoMerge` or `mergeBranch`.
  - A default-branch change: `gh repo edit --default-branch`, or a `gh api` PATCH of `repos/OWNER/REPO` with a `default_branch` field.
  - A `git push` that updates `master`, `main` (in any case) or the branch the local `origin/HEAD` names. The pushed branch is each refspec's destination: `dst` of `src:dst`, else `src`, with `+` and `refs/heads/` dropped. `HEAD` or `@` stands for the current branch, and a glob counts when it matches a default name. `--all`, `--branches` and `--mirror` push every branch. With no refspec, the push goes to the current branch's upstream (`branch.<name>.merge`), else to the current branch.

  Git state is read offline, in the call's directory as the PR-ready check finds it, or in the directory `git -C` names there. State git cannot read resolves nothing, so it never blocks on its own. A named default destination blocks everywhere. The line is read as the PR-ready check reads it: the same shells, command position, substitutions and data rules. So `gh pr view`, `gh pr merge --help`, and a commit message, heredoc body or `grep` pattern quoting a merge all pass. So do `git push origin feature/x`, `git push -u origin DYD-1-x`, a `gh api` GET of a PR, and `dydo merge` in the worker lane. A wrapper's command string (`bash -c`, `eval`, `pwsh -Command`, `cmd /c`, `env -S`, `ssh`, `watch`) is read, and refused for the route it holds. A Codex bare argv array is read as the same words in a shell: gh or git as its program, or a wrapper holding a route, is refused, while another program's arguments are data, so a bare `["grep", "-rn", "gh pr merge", "."]` passes as it does in every shell. This differs from the PR-ready check, whose bare-argv rule (admiral ruling 2026-10-27) still refuses another program's word that reads as a PR call. Every refusal names `dydo merge`, and the push and default-branch refusals also say the default branch is the user's. The blocks run with the hard blocks, right after the dangerous patterns and before every nudge, so a refusal never arms a warn's marker. A conflicted merge's local push to its feature branch passes. Accepted residuals, since the guard stops accidents, not a bypass:
  - An `-EncodedCommand`, or nesting past eight wrappers, is not decoded. Inside a git tree the PR-ready check refuses it unread; outside one it passes.
  - A GraphQL query or API body read from a file or stdin (`-F query=@file`, `--input`) is not read.
  - Aliases, functions, scripts and unrecognised programs are not followed.
  - A push whose destination only git config sets (a `remote.<name>.push` refspec, `push.default`) is judged by the rules above alone.
- **Agent-tool notice**: invoking the platform's built-in `Agent` tool passes with a stderr reminder that sub-agent calls run anonymous and governed by the same three layers.

## Housekeeping Rides Along

Because the guard runs on every matched tool call, it carries throttled maintenance jobs: a **daily validation** (config checks, report-only, never blocks).

---

## Integration for Other AI Tools

Any coding tool can integrate through two input modes:

- **Stdin JSON** (preferred for hooks) - the payload shown above; `file_path` for file tools, `command` for bash, `path` for search tools. Exit 0 with empty stdout allows; exit 0 with the deny decision on stdout blocks.
- **CLI arguments** (for testing) - `--action {edit|write|delete|read}`, `--path <path>`, `--command <command>`. Exit 0 allows; exit 2 blocks with the message on stderr.

---

## Related

- [Configuration Reference](../reference/configuration.md) - nudge format, off-limits
- [Architecture Overview](./architecture.md) - where the guard sits in the system
- [Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md) - why identity-gated enforcement left the guard
