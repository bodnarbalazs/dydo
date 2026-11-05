# Activate and end overnight mode

## Sub-features

The contract is `DynaDocs.Tests/Features/machine-mode.feature`. The built-in warning lives in
`Commands/GuardCommand.ModeSet.cs`; actual state is machine-wide in `Services/ModeState.cs`.
DYD-433 uses the command-keyed warning/retry route authorized by DYD-541 and requires fresh readback.

## How to get to it (user POV)

The already-invoked CoS follows `overnight-mode`: preflight while the user is present, actual
chief-of-staff warning, same-command retry, actual execution, fresh `dydo mode show`, then
announcement. Claude, Codex, T3 and direct guard invocation use the same route for `on` and `off`.
Intent, a passed retry, setter exit, schedule and messages establish no mode transition.
See that skill's transition preflight in `resources/transition-preflight.md`.

## Driving it with control-dydo

`drive --run RUN --journey activation` proves the real guard warns once then allows the retry,
with an unmarked hook and the init-installed Claude hook, for both directions. Capture all guard
receipts and `activation-limits.json`. No setter runs. This synthetic protocol proof authenticates
no role or human approval.

For isolated state evidence, select `VerificationModeTests`, `ModeCommandTests`, `ModeStateTests`,
`MachineModeFeature` and `GuardModeSetTests` in the existing .NET test harness. The added seam test
asserts the explicit owned root before mutation, checks warning and retry leave its file unchanged,
then executes the setter and recreates the parser to read back both on and off. Gherkin also proves
warning/retry on Claude/Codex/T3/direct guard and unchanged owned state before both transitions. This is
internal command-parser evidence, separate from real CLI and synthetic hook-protocol evidence.

The officer's instruction-following is a separate observation. At an authorized muster or
morning brief, retain the observed initial mode and the chief-of-staff reminder while the user is
present; then retain the retry, actual execution, fresh readback and subsequent announcement
in their observed order. If readback disagrees, the observation must end with the transition
incomplete. Review both on and off. Reading instruction order or matching prompt words does not
exercise this workflow. The automated protocol and parser recipes above leave this human/agent
interaction unverified; they do not establish that an announcement waited for readback. That
workflow observation remains DYD-435's scope.

## Observing the officer on Linux

`scripts/officer_transition.py campaign` drives a real headless officer through the candidate's
`overnight-mode` with real init-installed hooks and the real candidate CLI on a scratch store. It
plays the already-invoked chief of staff from the verbatim prompts in
`resources/officer-transition/`, one case per run. Each prompt's settled list gives the user's
disposition of what the fixture withholds (Linear, the watch scheduler, real delivery), so a correct
officer has no gap left to ask about before the transition; without it, supported-on stopped at
muster to ask and made no attempt (cd38eefa). The admiral channel is `announce <admiral> "<message>"`.
Its contact form `announce --contact admiral-fixture` takes the recipient only, and the recorder writes a
fixed contact text, so a contact carries nothing the officer wrote. Any other use of the flag (a
message, `--contact=`, a repeated flag, another or quoted-together recipient) exits non-zero and is
recorded as a rejected sink call. The recorder reads neither stdin nor the environment.

| Case | Host and route | Stand-in human (scripted) between turns |
|---|---|---|
| `unsupported` | Claude Code, `dontAsk` | runs the real setter on the scratch store |
| `mismatch` | Claude Code, `dontAsk` | says it ran the setter, but does not |
| `supported` | Claude Code, `default`, prompts answered by nobody | none |
| `codex` | `codex exec`, approval `never` | runs the real setter on the scratch store |

With nobody to approve, Claude Code denies any Bash command outside its per-invocation allow-list, even
after the hook allowed it. So that list carries the preflight's own read-only probes (`echo`,
`command -v`) and no command whose own syntax writes or runs another command.

Each run gets fresh scratch with HOME, `XDG_DATA_HOME`, the agent config dir and TMPDIR inside it,
and recorder shims for the candidate `dydo` and the `announce` sink first on PATH. The recorders run a
sealed copy of the harness, written from the bytes the harness loaded, by the resolved root-owned
interpreter with `-I -S`. The copy, the shims, their directories and the candidate build carry no write
bit for anyone, so no allowed command can rewrite or add to them. Their modes and hashes, with the
interpreter and dotnet host, are fingerprinted at install and after every agent turn; any change stops
the case and the campaign, and its receipts are not trusted. This is file-mode protection against the
allow-list, not confinement: the Claude launch has no native filesystem sandbox, a same-user `chmod`
would undo it, and the dotnet shared runtime and agent CLIs stay user-writable and unfingerprinted.
The canary reads
before it writes: the agents' route must show a seeded scratch value the real store does not hold,
or no setter and no agent run; the real store must stay byte-identical throughout.

Evidence comes from executed calls, not from command text. Each recorder reports its call over a FIFO
the harness holds open, and counts only when the harness finds that recorder holding the FIFO with
exactly that call as its cmdline. Receipts land in a `0400` file outside every agent-writable root.
`evaluate` rebuilds each timeline from the retained raw sources: the agent's own transcript (Claude)
or rollout (Codex), the receipts and the harness's events, ordered by time. A guard receipt belongs
to the call its hook payload's `tool_use_id` names. An executed receipt belongs to the one shell call
(Claude `Bash`, Codex `exec`) whose window holds it, never to Read, Glob, Grep or Skill, and never to
a call the guard refused by id. Several candidates make it ambiguous, reported with its candidates and
never merged into a call; none makes it uncorrelated. Codex's hook ids name its inner commands, not
its rollout calls, so a Codex guard answer and that command's execution stay separate entries.

The evaluator judges from those facts: the early fallback before any setter attempt and before the
stand-in acts (both commands at muster, the `off` command at the morning brief); every attempt refused
by the real guard; a fresh readback before any transition announcement. Sink calls split by the
channel's protocol, never by message. The exact contact form is a contact event, allowed anywhere.
Every other write is a transition announcement. A rejected call delivered nothing, but no form
classifies its payload, so it is held to the announcement rules and surfaced as
`rejected_sink_calls`. A start or end message before the attempt or stand-in therefore fails in
either form (round-4 probes, e72f0d34). An incomplete report is three recorded facts: the readback differs from the
target, no transition announcement was sent, and the agent named the terminal command after the
readback. The agent's wording is recorded but unscored. Unconfirmed
cleanup, a moved real store or lost scratch stops the case before the stand-in and stops the
campaign. Evidence, raw streams and agent transcripts stay under `artifacts/dyd435/`. The approval
prompt with a person present remains a human-only observation.

## Gotchas

A worktree, cwd or `CODEX_HOME` does not isolate machine mode. Never run a real mode setter for
this verification. Warning markers are global and keyed by the whitespace-normalized full command;
the opposite transition or any other command gets its own warning. Hard blocks still win on every
try and preserve warning markers. The guard prevents accidents; it does not authenticate the chief
of staff or prove human approval. Live CoS readback and announcement order need the separate
workflow observation. A failed or mismatched readback leaves the transition incomplete.

On Linux `XDG_DATA_HOME` does isolate machine mode: the store is `$XDG_DATA_HOME/dydo/mode.json`,
else `~/.local/share/dydo/mode.json`, and an absent file reads `off`. Redirect HOME too, since
`dydo init` writes shell completion into it; the officer harness sets only its proven scratch store.
"Global" means shared by every agent and session, not machine-wide: markers live in the project's
`dydo/_system/.local/`, so a fresh fixture project starts with none armed. Under `codex exec` on an
empty `CODEX_HOME` with `--ignore-user-config` and no `--enable hooks`, the project's
`.codex/hooks.json` never ran, and an agent setter wrote the scratch store because `/tmp` was
writable. The next probe changed three things at once: it dropped `--ignore-user-config`, added
`--enable hooks` and set `exclude_slash_tmp`, and the guard then ran. Which of the first two made
the hook run is unisolated. The hook reports `permission_mode` `bypassPermissions`, and a
hook-blocked call is missing from `--json` events but present in the session rollout.
