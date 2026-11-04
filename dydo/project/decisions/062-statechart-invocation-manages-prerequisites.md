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

Managed dependencies bind the dependency lock, their verified content and the
runtime identifier; the installed skill and extracted assets bind the payload and release.
An update that keeps the lock re-verifies the existing install and re-stamps it with no
download or consent. Content that fails verification yields an approval-required repair
plan that the normal continuation executes; nothing managed is replaced without it.
After an update, `install-skill` replaces an installed skill unchanged since its install and
leaves local edits untouched; extracted assets that fail verification are extracted again
offline from the embedded payload. A superseded lock's store stays, since another installed
dydo may still use it. Supported PS entry points remain until replacement proof covers applicable
interfaces and platforms. This decision states required behavior; a staged
engineering candidate does not itself prove delivery.

## Clarification - Desktop Approval Prompts - 2026-10-31

On Windows, Balazs reported three OpenJDK approval dialogs and clarified in
[DYD-415](https://linear.app/bodnar-balazs/issue/DYD-415) that genuine one-off preparation
may be acceptable, but routine statechart use and recurring tests must not require
administrator, firewall or other operating-system security dialogs. Normal host command
and installation authorization grants no authority to approve UAC, firewall or other
operating-system security prompts, bypass elevation, or change global security settings.
Hidden process creation does not show that no dialog appeared.

The Windows investigation found firewall rules for the managed Java executable paths and
an RMI listener in TLC's fingerprint set, a likely but unobserved prompt mechanism.
Prefer compatible existing runtimes or user-scoped archive installations, and let tests
reuse a verified runtime's stable path when their assertion allows. A stable path does not
remove TLC's listener. Checking therefore routes TLC through the loopback bootstrap: on
Linux, TLC through it listens only on `127.0.0.1`, where raw TLC listens on every interface
(verified on DYD-415, 2026-11-04). The Windows-era record is kept at
[67cd2127](https://github.com/bodnarbalazs/dydo/tree/67cd21278872679398479601b99d95125678b7cd/dydo/project/pitfalls/dyd-415-statechart-evidence).

## Related

- [Decisions](./_decisions.md)
- [Statechart verification](../pitfalls/dyd-415-statechart-verification.md)
