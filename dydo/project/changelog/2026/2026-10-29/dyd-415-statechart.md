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

The formal-verification skills now name this companion and keep its inputs tied to
the checked model and verification sync commit. Both Claude and Codex require user
invocation. Native PowerShell and Git Bash support includes Windows process cleanup
and CRLF handling. The complete pinned pstack runtime, corpus and MIT license ship
with the skill.

## Related

- [Verification evidence](../../../pitfalls/dyd-415-statechart-verification.md)
- [Control flow](../../../../understand/control-flow.md)
