---
area: general
type: decision
status: accepted
date: 2026-10-30
---

# 062 - Statechart Invocation Owns Prerequisites and Continuation

Agents invoke the statechart capability with the original model inputs. The
capability owns dependency discovery, a concrete first-use installation plan,
normal host authorization, managed verification and continuation of that request.
TLC remains the upstream checker. This decision applies only to statechart.

## Context

The source skill required agents to find and provision Java, TLC, a compatible
JavaScript runtime and renderer. Mandatory separate setup did not satisfy the
intended agent journey. Balazs ruled in
[DYD-415](https://linear.app/bodnar-balazs/issue/DYD-415#comment-9217199a-a9b0-4ede-976a-192846e9bc83):

> It doesn't give me the command to run it, the agents do it. The agents need it.
> But yes, retain the upstream checker.

> Agents shouldn't have to worry about any of the setup that's necessary.

## Decision

1. A ready invocation runs offline with verified compatible dependencies and no
   repeated installation or consent. Explicit invalid overrides are errors.
2. Missing dependencies produce a precise locked plan, structured event and saved
   original request, exit3. The proposed continuation is
   `dydo statechart resume --request ABSOLUTE_REQUEST_JSON`.
3. The installed skill submits that exact continuation through the host's normal
   command and installation authorization mechanism. An ordinary CLI has no host
   permission interface. A local flag, token or receipt is not host approval.
4. An authorized continuation verifies immutable request/payload/lock identities,
   installs missing locked assets into owned roots, proves readiness, publishes
   its receipt atomically and continues the saved inputs. No mandatory manual
   setup or reconstruction of the model request is required.
5. Denial, unavailable authorization, installation failure and cancellation remain
   non-success and preserve the request. No checker starts on an unverified plan.
6. Checking runs under native statechart ownership, with registered scratch and
   startup gates. Only authoritative drain authorizes registered-owned cleanup;
   uncertainty retains scratch and a durable reason. A notification alone does
   not establish drain. No general supervisor or installer policy is introduced.

## Consequences

The normal ready journey stays one invocation. First use may need a second
host-authorized tool action because no in-process host approval interface is
assumed. Optional doctor/setup interfaces share the capability's rules rather
than delegate prerequisite choreography to the agent.

Installed resources bind the exact skill/runtime/installer payload and dependency
lock. Supported PS entry points remain until replacement proof covers applicable
interfaces and platforms. This decision states required behavior; a staged
engineering candidate does not itself prove delivery.

## Clarification - Desktop Approval Prompts - 2026-10-31

Balazs reported three OpenJDK approval dialogs and clarified that genuine one-off
preparation may be acceptable, while recurring tests and normal statechart use
must not require administrator or firewall approval dialogs. This statechart-only
clarification is governed by the live [DYD-415 ruling](https://linear.app/bodnar-balazs/issue/DYD-415).
The retained [source-only attribution](../pitfalls/dyd-415-statechart-evidence/native-invocation/implementation3/desktop-prompt-attribution/assessment.md)
records six Query User firewall rules for exactly three managed Temurin executable
paths: installer staging, final installation and a readiness-test copy. The pinned
TLC fingerprint-set constructor calls the exporting RMI superclass constructor.
These facts support a concrete firewall/listener mechanism; individual prompt
timestamps and direct dialog classification remain unobserved. Normal
host command/installation authorization grants no authority to approve UAC,
firewall or other operating-system security prompts, bypass elevation, or change
global security settings. Hidden process creation proves no absence of dialogs.

Prefer compatible existing runtimes or user-scoped archive installations. Tests
should reuse a verified runtime's stable executable path where their assertion
does not require a fresh runtime copy. Neither choice establishes prompt-free
behavior without evidence: stable Java paths do not remove TLC's first-use RMI
listener. An advertised RMI hostname does not establish a loopback-only bind.
The later [source addendum](../pitfalls/dyd-415-statechart-evidence/native-invocation/implementation3/posix-source/source-addendum.md)
records the documented loopback socket-factory direction before its pre-code pin.
The subsequent [captain pin](https://linear.app/bodnar-balazs/document/dyd-415-rmi-bootstrap-pre-code-pin-and-windows-access-checkpoint-4da014a5815f),
implementing [admiral disposition](https://linear.app/bodnar-balazs/issue/DYD-415#comment-7b3e3281-f30e-46c2-b649-ac9d68353243),
authorizes Stage A original Java bootstrap, recipe and fixture source only.
The [source checkpoint](../pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/checkpoint.md)
records the unbuilt artifact, unchecked standalone TLC override and finite scope
dependency. Source authoring supplies no prompt-free runtime proof. Captain
inspection and a separate contained POSIX release precede compiler/bootstrap
execution. The global factory does not constrain explicit per-object factories
or sandbox arbitrary model extensions and injected JVM agents. The checker,
BFS, action constraints, liveness semantics and one-worker bound stay required.

The adopted collector has no Java language mapping or clone row. Java line and
branch coverage, CC, cognitive complexity, HCRAP, parameter, nested-ternary,
dead-code, clone and cycle measurements are unavailable through that mechanism.
Warning-clean compilation and behavioral fixtures cannot substitute for those
measurements or establish a full static PASS. An explicit applicable disposition
is owed before readiness; the earlier DYD434 decision supplies no waiver.
No isolated Windows target with a clean executable-policy baseline and adequate
prompt/event visibility is confirmed. Windows acceptance remains access-blocked.
Probe, install and Java-launch sequences that might
produce another dialog remain held pending explicit captain disposition; source
and retained-log analysis must not reproduce the prompt. No occurrence is called
one-off before attribution.

The [Stage A closure and B1 result](../pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031a/result.md)
record the later source correction requiring pinned TLC bytes for explicit,
discovered and direct checker inputs, plus a dual-table/process-owned socket
observation fixture. These source changes have no runtime verdict. B1 failed
before FIFO admission because sandbox `LOCALAPPDATA` selected another namespace
and its holder update was denied. No test, compiler, Java or container ran;
the reviewed build lock remains unbuilt and payload declarations are unchanged.

## Related

- [Decisions](./_decisions.md)
- [Statechart verification](../pitfalls/dyd-415-statechart-verification.md)
