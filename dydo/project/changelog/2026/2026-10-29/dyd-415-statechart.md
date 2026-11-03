---
area: project
type: changelog
date: 2026-10-29
---

# DYD-415: checked statecharts for formal verification

Added the user-invoked `statechart` skill for dydo 3.6.0. It runs TLC on a bounded
TLA+ model and writes a Mermaid chart, shortest bug paths, a report and raw checker
output. Above 500 states it draws only bug paths, and writes no chart when all rules
hold. Pele renders charts to SVG.

`dydo statechart` checks a model on Windows and Linux with the agent's original inputs.
When Node, Java, TLC or Pele is missing, it saves the request and exits `3` with a locked
plan; `dydo statechart resume --request` installs the locked versions (Node 26.13.0,
Temurin JRE 21, TLC 1.7.4, Pele 1.9.0) through the host's normal approval and continues
the same chart. `dydo statechart install-skill` installs the matching skill into a project.

The formal-verification skills now name this companion and keep its inputs tied to
the checked model and verification sync commit. Both Claude and Codex require user
invocation. The skill keeps its PowerShell and POSIX launchers, Windows process cleanup
and CRLF handling. The pinned pstack runtime and MIT license ship with the skill; its
test corpus stays in this repository.

## Related

- [Verification notes](../../../pitfalls/dyd-415-statechart-verification.md)
- [Control flow](../../../../understand/control-flow.md)
