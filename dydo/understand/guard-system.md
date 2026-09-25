---
area: understand
type: concept
---

# Guard System

How dydo enforces boundaries through the PreToolUse hook. Every tool call the hook's matcher names — reads, writes, searches, bash, in the main thread and inside every subagent and workflow — passes through `dydo guard` before execution. Three layers: path tiers (off-limits and protected), dangerous-bash detection, and nudges.

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

- **Exit 0** — action allowed (a `NOTICE:` or `WARNING:` on stderr may ride along)
- **Exit 2** — action blocked (`BLOCKED: <reason>` on stderr, tool fails)

`dydo init` installs the matcher that decides which calls arrive: on Claude `Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit|AskUserQuestion`, on Codex `Bash|apply_patch|Edit|Write|Agent|shell_command|exec|local_shell|unified_exec`. A tool outside its host's matcher never reaches the guard.

There is no identity, no staging, no per-role permission matrix: the path tiers, dangerous-command rules and command nudges apply to every caller, every time ([Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)). One caller *kind* is distinguished, and it is not a role: a call carrying `agent_id` is a sub-agent, which additionally may not run `dydo` commands.

---

## Layer 1: Path Tiers

`dydo/files-off-limits.md` declares two tiers, both applying to every caller.

**Off-limits** patterns hard-block **every** operation — read, write, search, or bash.

- **Covered:** secrets and credentials (`.env*`, `*.pem`, `*.key`, `secrets.json`, database configs) plus the hardcoded `dydo/_system/**` machine state.
- **Whitelist:** a `## Whitelist` section carves exceptions (e.g. `.env.example`) to the configured patterns — never to the hardcoded ones.
- **Patterns** use glob syntax: `**/` for optional directory prefix, `**` for any path, `*` within a segment, `?` for a single character; matching is case-insensitive on every platform.
- The one hardcoded exemption is the platform's native auto-memory directory outside the repo.

```
BLOCKED: Path is off-limits to all agents.
  Path: .env
  Pattern: .env*
  Configure exceptions in dydo/files-off-limits.md
```

**Protected** patterns (a `## Protected Patterns` section) invert the emphasis: **every agent may read them, none may write or delete them** ([Decision 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) §10). Membership is dydo's own system files — `dydo/index.md`, `dydo/files-off-limits.md` and the hardcoded `dydo.json` — because agents read them to orient themselves, `dydo/index.md` on every entry prompt's order. The tier's contract is that no agent writes them *directly*, and a human owns their content; dydo's own commands still rewrite what they manage — `dydo index` regenerates `dydo/index.md`, and `dydo fix` writes `dydo.json`. Those arrive as `dydo` command lines with no file path to extract, so they pass: the tier stops hand edits, not dydo's tooling. `CLAUDE.md`, `AGENTS.md` and harness config files stay outside the guard entirely: the harness defends its own files, and off-limits keeps its original meaning of files agents must not even read.

The tier binds on the mutating call only: the `Edit`, `Write` and `NotebookEdit` tools, Codex's `apply_patch`, a CLI `--action` of `edit`, `write` or `delete`, and every operation the bash analyzer extracts from a shell command that is not a read of that path — write, delete, move, copy, permission change. `Read`, `cat`, `head` and search pass. The whitelist does not apply.

```
BLOCKED: Path is protected — every agent may read it, none may write or delete it.
  Path: dydo/index.md
  Pattern: dydo/index.md
  Detected: Write via sed -i
  This file is human-owned: read it freely, and ask the human for any change.
```

---

## Layer 2: Bash Command Analysis

Bash commands get deeper treatment than direct tool calls:

1. **Dangerous pattern detection** (immediate block): recursive root/home deletes, fork bombs, direct disk writes (`dd`), download-and-execute (`curl | sh`), base64 decoded into an interpreter, eval of variable content, history clearing, security disables (SELinux, firewall), shadow/passwd access, and inline interpreter execution (`python -c`), which would hide file operations from the analysis below. The built-in list also blocks destructive Git pushes and recovery removal, destructive GitHub CLI commands, disk formatting, privileged deletes, system-wide permission changes, and access to password managers or secret-key exports. These rules cannot be configured away.
2. **Nudges**: Layer 3 is evaluated here, on the raw command text, before any path is extracted.
3. **Chained `cd` block**: `cd /path && command` breaks path analysis — run `cd` separately or use absolute paths. Skipped for dydo's own commands.
4. **Bypass detection**: command substitution (`$(...)`), base64/hex decode, variable expansion, embedded newlines — flagged because they can obscure the paths actually being touched. On their own each is a `WARNING:` on stderr and the command proceeds; combined with any extracted write, delete, move, copy or permission change the command is **blocked outright**, because the path it would touch cannot be verified. So `cat $FILE` warns and runs, while `echo x > $OUT` exits 2.
5. **File operation extraction**: the command is tokenized into reads (`cat`, `grep`), writes (`tee`, `>`, `>>`, `sed -i`), deletes (`rm`), copies/moves (`cp`, `mv`), and permission changes (`chmod`) — and each extracted path is checked against off-limits — and, for anything that is not a read of it, the protected tier — individually. A chain can't smuggle a guarded path past the guard.

The guard fires on `dydo` commands themselves too — dangerous patterns, nudges and the path checks apply to dydo's own CLI like anything else; only the `cd` coaching is skipped for them.

### David Ondrej denylist audit

DYD-188 compared [the upstream denylist](https://github.com/davidondrej/skills/blob/main/hooks/dangerous-patterns.txt) (blob `02e930aaf9879b879475baff5142f78e2df52f2a`) and its [test matrix](https://github.com/davidondrej/skills/blob/main/hooks/test-guard.sh) with the built-in analyzer. The following table records the disposition of each family. Command-shaped detections use upstream's anchor: the command name, optionally path-qualified (`/usr/bin/dd`), starts the line or follows whitespace or a shell separator (`;`, `&`, `|`, `(`, `)`, `{`, `}`, a backtick, `!`, `\`). So `sudo`, `nice`, `xargs`, `ssh host`, assignments such as `LC_ALL=C` and keywords such as `then` need no wrapper list. A quote starts a command only after a shell `-c` flag or an `ssh` remote command, so `echo 'dd ... of=/dev/loop0'` is allowed. `dd` writes to `sd*`/`nvme*`/`vd*`/`mmcblk*` targets still block anywhere in the command text, as before. A command named later inside quoted prose follows whitespace and is blocked, as upstream does: `echo 'sudo dd ... of=/dev/sda'` fails closed. Every rule has a 250 ms match timeout; a command that exceeds it is blocked as too complex to verify. Option values and path targets limit false positives.

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
| Generic `git reset` and `git clean` | Excluded: absent from upstream and local, recoverable operations outside this Issue's contract | Existing path guard still evaluates extracted operations |

The upstream POSIX expressions are translated to .NET regexes rather than copied verbatim. The hook evaluates these before nudges or path analysis, so a matching command blocks for every caller even when it names no local file.

---

## Layer 3: Nudges

Nudges are project-configurable rules in `dydo.json`: a pattern plus a message, at one of three severities.

| Severity | Behavior |
|----------|----------|
| `notice` | `NOTICE:` on stderr, never blocks (exit 0) |
| `warn` | Blocks once with "(Run the same command again to proceed anyway.)"; the retry passes. The pass-through marker lives in `dydo/_system/.local/` (gitignored), keyed by pattern hash. |
| `block` | Always blocks |

- **Command nudges** — regex matched against bash command text. Capture groups substitute into the message (`$1`, `$2`, …).

The shipped **review-block nudge** is the one that carries policy: a `gh pr create` whose command text has no `Independent review` in it is warned once, because nothing reaches the human that an independent agent has not reviewed and the PR body is where that proof lands ([Decision 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md) §3). At `warn` an honest exception costs one retry; raising it to `block` is a human's edit to `dydo.json`, made only if the discipline erodes.

**Shipped defaults and self-healing:** the indirect-dydo-invocation nudges (`npx dydo`, `dotnet dydo`, `python dydo`, …) are severity-pinned — `MergeSystemNudges` reconciles config against the shipped set on every nudge evaluation, in memory and without rewriting `dydo.json`: a deleted block-default is re-added, a downgraded severity is restored to `block`, and a nudge still carrying a known-stale shipped message is healed to the current text or dropped if its default was retired. A message the user customized matches no known-stale text and is never clobbered.

---

## Also Enforced

- **Plan-mode block**: `EnterPlanMode`/`ExitPlanMode` are blocked — planning happens through the Wayfinder or Code Writer skill and plan records, not the platform's plan mode.
- **Agent-tool notice**: invoking the platform's built-in `Agent` tool passes with a stderr reminder that sub-agent calls run anonymous and governed by the same three layers.

## Housekeeping Rides Along

Because the guard runs on every matched tool call, it carries throttled maintenance jobs: a **daily validation** (config checks, report-only, never blocks).

---

## Integration for Other AI Tools

Any coding tool can integrate through two input modes with the same contract (exit 0 allows, exit 2 blocks with stderr message):

- **Stdin JSON** (preferred for hooks) — the payload shown above; `file_path` for file tools, `command` for bash, `path` for search tools.
- **CLI arguments** (for testing) — `--action {edit|write|delete|read}`, `--path <path>`, `--command <command>`.

---

## Related

- [Configuration Reference](../reference/configuration.md) — nudge format, off-limits
- [Architecture Overview](./architecture.md) — where the guard sits in the system
- [Decision 041](../project/decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md) — why identity-gated enforcement left the guard
