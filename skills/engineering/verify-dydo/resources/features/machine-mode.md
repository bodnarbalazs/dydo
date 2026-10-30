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

## Gotchas

A worktree, cwd or `CODEX_HOME` does not isolate machine mode. Never run a real mode setter for
this verification. No installed marker, unsupported permission mode or unknown capability means
human-terminal fallback, not a spoofed marker or retry-until-bypass. Genuine approval, rejection
in the UI and the live CoS readback remain unverified until a user-authorized workflow observes
them. No production security change is proposed: proper Codex approval enforcement needs a
separate product/security decision backed by native-host evidence.
