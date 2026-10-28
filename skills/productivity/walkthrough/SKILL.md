---
name: walkthrough
description: Show me the product as it stands - what changed since the last walkthrough, where to look first, and how to try it. Load only when the user asks, or at the map holder's Milestone or landing step.
---

# Walkthrough

Give the user the tour of the whole product as it stands, so it makes sense to him even when he is
not close in.

A Project has one Walkthrough Ticket per Milestone, the landing's last. At a Milestone, walk the
feature branch at the SHA its last merge left; at the landing, walk main after it landed. The map
holder runs the ticket with the user; he walks it, or skips it, and a skip is his verdict to go on.

The argument names what to walk - a Milestone, a Project's landing, a feature branch, a ticket, a
diff range. Ask which one when it is missing.

Walk it yourself first: the whole state at that SHA, the diff since the last walkthrough and its
hops, the tickets it closed with their review blocks, the DRs and applicable spec sections those
tickets cite, the returns their crew posted. Then brief the user in four parts, the newest changes
first, and mark the ones that need his careful look: where a drift from his intent would cost most,
or a review left doubt.

- **What changed and why** - the current state in one paragraph, then what changed since the last
  walkthrough, each part traced to the ticket that carried it and the DR that decided it, with the
  shape drawn by `show-me` where a tree or a diff says it faster than prose.
- **Where to look first** - a route for his own file-by-file pass: the parts marked for a careful
  look, the files that drew findings, then the ones a crew member left an open doubt on, then the
  seams the change moved.
- **How to try it** - the exact commands to build it and see the new behaviour, in order, each one run
  once by you so it is true when he types it.
- **What reviewers flagged or deferred** - in the severities the Communication and evidence section
  of `dydo/reference/linear-workspace-standard.md` defines: each `should` deferred and the ticket
  where it now lives, reported as it stands. At the landing, also every unaddressed `nit` with the
  ticket record where it sits, for the user to promote any.

The tour ends when the user has inspected the files and tried the behavior, or skipped it. Record
his verdict on the Walkthrough Ticket in his own words, or the skip. Other tours route findings
through ordinary tickets. A Milestone's findings become tickets in the same Project as they arrive.
At the landing, record findings as tickets in the same Project; the map holder commissions the
first fix Captain to re-cut its feature branch from main for fixes and another landing/walkthrough.
An empty landing walkthrough lets the Project close; the map holder commissions spec retirement
following `dydo/reference/feature-spec-convention.md`, read from the repository root.

Keep the brief in the terminal, or in a scratch file when the user wants to read it beside the diff.
The durable record stays where it already is: the tickets, the review blocks and the DRs the tour
points at.
