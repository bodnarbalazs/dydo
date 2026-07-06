---
id: 195
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-05-19
resolved-date: 2026-07-04
---

# Duplicate-wait DoS: an attacker with DYDO_AGENT=X can hold X's general-wait slot indefinitely from a plain shell

WaitCommand.WaitGeneral creates a Listening marker for the hijacked agent with the calling process's PID (line 108) and the duplicate-wait refusal at line 85-91 keys on whatever agent the caller resolves to. A process that sets DYDO_AGENT=X and runs dydo wait once writes a Listening marker for X with its own PID; the ancestor-death gates at lines 141-146 do not fire when the attacker runs from a plain shell with no claude ancestor, so the marker persists. Every subsequent dydo wait for X (including X's legitimate terminal's re-arm) exits 2. New hijack-class variant — withholds wait availability rather than mutating state. Closes alongside F1 (issue #0183) if the wait command verifies caller ownership at CreateListeningWaitMarker; otherwise needs its own ownership check.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed at HEAD: WaitCommand refuses to register a wait marker unless the caller owns the resolved agent (WaitCommand.cs:52-56 VerifyCallerOwnsAgent; AgentRegistry.cs:999-1009 'Closes #0195'; spot-verified). Triage sweep 2026-07-04 (Brian, CoS).