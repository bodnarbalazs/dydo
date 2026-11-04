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
See that skill's transition preflight in `resources/approval-preflight.md`.

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

## Gotchas

A worktree, cwd or `CODEX_HOME` does not isolate machine mode. Never run a real mode setter for
this verification. Warning markers are global and keyed by the whitespace-normalized full command;
the opposite transition or any other command gets its own warning. Hard blocks still win on every
try and preserve warning markers. The guard prevents accidents; it does not authenticate the chief
of staff or prove human approval. Live CoS readback and announcement order need the separate
workflow observation. A failed or mismatched readback leaves the transition incomplete.
