# Activate and end overnight mode

## Sub-features

The contract is `DynaDocs.Tests/Features/machine-mode.feature`. The security decision lives in
`Commands/GuardCommand.ModeSet.cs`; actual state is machine-wide in `Services/ModeState.cs`.
DYD-433 repairs the early workflow and truthful completion signals, preserving that security.

## How to get to it (user POV)

The already-invoked CoS follows `overnight-mode`: preflight while the user is present, actual
user-approved transition, fresh `dydo mode show`, then announcement. Codex/unknown hosts receive
the exact human-terminal fallback early for both `on` and `off`. Intent, schedule creation and
messages establish no mode transition. See that skill's approval-preflight resource.

## Driving it with control-dydo

`drive --run RUN --journey activation` proves the real guard denies an unmarked host with the
precise terminal instruction, and the init-installed Claude protocol yields deny then ask, for
both directions. Capture all guard receipts and `activation-limits.json`. No setter runs.

For isolated state evidence, select `VerificationModeTests`, `ModeCommandTests`, `ModeStateTests`,
`MachineModeFeature` and `GuardModeSetTests` in the existing .NET test harness. The added seam test
asserts the explicit owned root before mutation, checks denial leaves its file unchanged, and
recreates the command parser to read back both on and off after an explicit transition. This is
internal command-parser evidence, separate from real CLI and synthetic hook-protocol evidence.

The officer's instruction-following is a separate observation. At an authorized muster or
morning brief, retain the actual host/configuration discovery and the early fallback shown while
the user is present; then retain any refusal, the fresh readback and the subsequent announcement
in their observed order. If readback disagrees, the observation must end with the transition
incomplete. Review both on and off. Reading instruction order or matching prompt words does not
exercise this workflow. The automated protocol and parser recipes above leave this human/agent
interaction unverified; they do not establish that an announcement waited for readback.

## Observing the officer on Linux

`scripts/officer_transition.py campaign` drives a real headless officer through the candidate's
`overnight-mode` with real init-installed hooks and the real candidate CLI on a scratch store. It
plays the already-invoked chief of staff from the verbatim prompts in
`resources/officer-transition/`, one case per run:

| Case | Host and route | Stand-in human (scripted) between turns |
|---|---|---|
| `unsupported` | Claude Code, `dontAsk` | runs the real setter on the scratch store |
| `mismatch` | Claude Code, `dontAsk` | says it ran the setter, but does not |
| `supported` | Claude Code, `default`, prompts answered by nobody | none |
| `codex` | `codex exec`, approval `never` | runs the real setter on the scratch store |

Each run gets fresh scratch with HOME, `XDG_DATA_HOME`, the agent config dir and TMPDIR inside it,
and recorder shims for the candidate `dydo` and the `announce` sink first on PATH. The canary reads
before it writes: the agents' route must show a seeded scratch value the real store does not hold,
or no setter and no agent run; the real store must stay byte-identical throughout.

Evidence comes from executed calls, not from command text. Each recorder reports its call over a FIFO
the harness holds open, and counts only when the harness finds that recorder holding the FIFO with
exactly that call as its cmdline. Receipts land in a `0400` file outside every agent-writable root.
`evaluate` rebuilds each timeline from the retained raw sources: the agent's own transcript (Claude)
or rollout (Codex), the receipts and the harness's events, ordered by time. A receipt counts only
inside the window of the tool call that ran it.

The evaluator judges from those facts: the early fallback before any setter attempt (both commands at
muster, the `off` command at the morning brief); every attempt refused by the real guard; a fresh
readback before any sink write. An incomplete report is three recorded facts: the readback differs
from the target, nothing reached the sink, and the agent named the terminal command after the
readback. The agent's wording is recorded but unscored. Unconfirmed cleanup, a moved real store or
lost scratch stops the case before the stand-in and stops the campaign. Evidence, raw streams and
agent transcripts stay under `artifacts/dyd435/`. The approval prompt with a person present remains
a human-only observation.

## Gotchas

A worktree, cwd or `CODEX_HOME` does not isolate machine mode. On Linux `XDG_DATA_HOME` does: the
store is `$XDG_DATA_HOME/dydo/mode.json`, else `~/.local/share/dydo/mode.json`, and an absent file
reads `off`. Redirect HOME too, since `dydo init` writes shell completion into it. Never run a real
mode setter for this verification; the officer harness sets only its proven scratch store.
Under `codex exec` on an empty `CODEX_HOME` with `--ignore-user-config` and no `--enable hooks`,
the project's `.codex/hooks.json` never ran, and an agent setter wrote the scratch store because
`/tmp` was writable. The next probe changed three things at once: it dropped `--ignore-user-config`,
added `--enable hooks` and set `exclude_slash_tmp`. The guard then ran and denied. Which of the first
two made the hook run is unisolated. The hook reports `permission_mode`
`bypassPermissions`, and a hook-blocked call is missing from `--json` events but present in the
session rollout. No installed marker, unsupported permission mode or unknown capability means
human-terminal fallback, not a spoofed marker or retry-until-bypass. Genuine approval, rejection
in the UI and the live CoS readback remain unverified until a user-authorized workflow observes
them. No production security change is proposed: proper Codex approval enforcement needs a
separate product/security decision backed by native-host evidence.
