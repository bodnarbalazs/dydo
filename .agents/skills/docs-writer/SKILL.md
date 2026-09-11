---
name: docs-writer
description: Documentation the repository can witness. Write or correct one reviewed change, including an inquisition's record through its delivery Feature, for its Issue Captain.
---

# Docs Writer

Make one documentation change true.

## Must-Reads

1. The owning Linear Issue and its linked Project plan, when present.
2. [writing-docs.md](../../../dydo/reference/writing-docs.md)
3. [about.md](../../../dydo/understand/about.md)
4. [working-tree-contract.md](../../../dydo/guides/working-tree-contract.md)
5. [Communication and evidence](../../../dydo/reference/linear-workspace-standard.md#communication-and-evidence) — read only this section for the communication protocol; do not preload the whole standard.

## Boundary

Every sentence you write is a claim, and every claim needs a **witness** in the repository: code,
configuration, a Decision Record, the Issue. Where the witness is missing, return the gap instead.
The Issue Captain that spawned you owns review, integration, status and follow-up work; you own the
words.

## Method

1. **Find the witness.** Read the code, configuration and governing decisions behind the change
   until every claim you mean to write has one.
2. **Write it in its one home.** Concepts in `understand/`, procedures in `guides/`, exact contracts
   in `reference/`, delivery history under `project/`; a meaning another document owns is a link.
   A dydo document takes its frontmatter and links from writing-docs.md; an agent-facing
   document (a skill template, its resources, an entry point) is written by `writing-for-agents`.
   Cut whatever the code already states plainly. Done when each claim has one canonical home,
   repeated meanings are pointers, and every added claim has its witness.
3. **Edit the source; regeneration writes the rest.** Author navigation pages when the docs need them;
   compiled skills under `.claude/`, `.codex/` and `.agents/` are `dydo sync`'s. Done when every edit
   is in its authored source and required regeneration is either verified in scope or explicitly
   handed to the integration owner with its source paths and command.
4. **Write the inquisition record** when the separate record Feature's captain hands you its
   contract and the Inquisition's pinned evidence packet. Work on that Feature's branch, following
   the working-tree contract's retention route: a document in
   `dydo/project/inquisitions/` naming scope and feature SHA, parts and lenses swept, findings,
   hypotheses with verdicts, and Bugs filed with their reproduction commits. Done when every claim
   traces to the packet, with an empty section explicitly recorded as such; return the exact path
   and blob or content digest for the delivery review and the Inquisition captain's later check.
5. **Fix, check, commit.** `dydo fix` after a document is added, moved or renamed, then this hop's
   proof until clean: `dydo check`, the other cheap checks the Issue names, and the tests your change
   reaches. Then commit in the owned paths; the full suites wait for the gate the Captain names. Done
   when that focused proof has run and the work is committed.

## Return

To the Issue Captain, use the `IMPLEMENTED` form in the communication protocol, naming the SHA,
files, witness, checks, gaps, and evidence. A writer's successful delivery is never an independent
`PASS`. For an inquisition record, name its path and preserved evidence.
