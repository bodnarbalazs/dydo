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

## Related

- [Decisions](./_decisions.md)
- [Statechart verification](../pitfalls/dyd-415-statechart-verification.md)
