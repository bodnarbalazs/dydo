---
area: project
type: decision
status: accepted
date: 2026-11-04
accepted: 2026-11-04
participants: [balazs, Claude ticket-captain]
---

# 063 - `dydo survey`: a Read-Only Lane Digest

`dydo survey <project>` reads one Linear Project on demand with the user's `LINEAR_API_KEY` and
prints a short, deterministic digest of its lanes. It never writes, caches, polls, subscribes to or
mirrors Linear. It follows [DR 052](./052-dydo-map-read-only-linear-view.md)'s rules for a dydo
command that reads Linear. Balazs settled it on 2026-11-04 in the
[DYD-520](https://linear.app/bodnar-balazs/issue/DYD-520) thread: "Let's build the dydo survey. It's
good because linear is already a dependency."

---

## Context

DYD-520's admiral check-in has a surveyor read the whole Project board every 20 minutes. Half of
that read is deterministic graph work: which tickets just became unblocked, which sit in no lane,
which ledger rows are stale. An LLM that does it through dozens of MCP calls is slow and can miss
things. Misses 1, 2 and 5 on DYD-520 were exactly this kind of miss.

DR 052 made `dydo map` the one dydo command that reads Linear and named it as the exception in
every boundary text. A second reading command needs its own decision.

## Options Considered

### Option A: Keep the read in the surveyor's prompt

- **Pros:** no new command; the boundary texts stay as they are.
- **Cons:** slow, many MCP calls, and the misses DYD-520 recorded keep happening.

### Option B: A synchronized local copy of the board

- **Pros:** instant answers.
- **Cons:** a mirror, which DR 044 forbids, with the polling and staleness it retired.

### Option C: A read-only command that reads on demand (chosen)

- **Pros:** exact, a few lines of output, nothing kept in step; it reuses `dydo map`'s reader, so
  dydo gains no second Linear client.
- **Cons:** each run costs one round of API reads, and the command needs the user's key.

## Decision

1. **DR 052's rules.** Read-only and on demand: the caller decides when to run it. The user's key in
   `LINEAR_API_KEY`; without one it prints the missing-key help it shares with `dydo map` and exits 2
   before any request. No mirror, cache, polling or subscription.
2. **One reader.** It reads through `dydo map`'s `LinearReader`: the Project's non-archived issues,
   the linked far ends outside it, and their `blocks` and `related` relations. The survey adds the
   Project body (`content`) in one more query, each issue's priority, and each Project issue's parent
   identifier, so a parent outside the graph still lanes its sub-issues. `dydo map`'s output is
   unchanged. Labels are read as the map reads them, the first ten per issue, so a `Question` label
   past an issue's tenth is not seen.
3. **The ledger.** The ledger is the body section under the first `##` heading containing "ledger"
   (any case), up to the next `##` heading, read as markdown. A ledger row is a table row, with or
   without edge pipes, or a list item with its continuation lines and the prose under it, quotes
   included; a table's separator row is none. A list item's nested list items and table rows are rows
   of their own. Code holds no rows, no identifiers and no headings. The issues a row names are every
   issue identifier in it, bare or inside a Linear link. Paragraph text in the section names nothing.
   A table row's `State` cell is its cell in the column under a `State` header.
4. **The findings.** Closed means a `completed`, `canceled` or `duplicate` state; everything else,
   backlog and triage included, is open. Each finding is one line, `<kind> <KEY> <title> — <reason>`,
   sorted by kind and then key:
   - `unblocked`: an open, unassigned issue in the Project or linked from it, with at least one
     `blocks` relation into it, where every blocker is closed. For a linked issue the reason says
     every blocker in the Project's graph is closed, since its blockers elsewhere are not read.
   - `pickable`: the map viewer's rule (unstarted, unassigned, no open blocker), minus issues already
     listed as `unblocked`.
   - `unlaned`: an open Project issue the ledger doesn't name, unless the ledger names its parent.
   - `stale-row`: a ledger row whose named issues in the read graph are all closed, while the row
     doesn't read as done. Only identifiers in the graph count, so a token such as `UTF-8` or an issue
     elsewhere neither saves nor judges a row; a row naming none is not judged. A row reads as done
     only when it is a table row whose `State` cell is `done`, trimmed, in any case, backticks
     allowed. A list item, or a row of a table with no `State` column, never reads as done. The
     ledger's own structure decides, not its prose: a word match can always be fooled, as "not fully
     done" or "waiting to be merged" showed at review round 2.
   - `started-unassigned`: a started-type Project issue with no assignee.
   - `question-no-priority`: an open Project issue labelled `Question` with no priority.
   - `uncontracted`: an unassigned Project issue in a `backlog`-type state with no open blocker, with
     the reason "Backlog, unassigned, no open blocker". The admiral contracts it to `Todo`
     ([DYD-520](https://linear.app/bodnar-balazs/issue/DYD-520) ruling 8). It is never `pickable`.
   - `no-ledger`: the body has no ledger section. Its key is `-` and its title the Project's name.
     Every open Project issue is then `unlaned`.
5. **Exit codes.** A successful read exits 0, findings or not. An argument that is not one Project
   UUID, slugId or Linear Project URL, or an unknown Project, is a usage error and exits 2. A missing
   key, or an API or network failure, exits 2 with a message naming the failure; a ticket that
   vanishes mid-read is named as that ticket, not as an unknown Project.

## Consequences

- **Gained:** the surveyor gets the deterministic half of its read in one command and keeps only
  the liveness check.
- **Accepted:** dydo now has two commands that read Linear. Both read through one client, and both
  are bounded by DR 052's rules. The boundary texts that named `dydo map` as the only reader now
  name both.
- **Accepted:** the graph holds only the relations that touch the Project. A linked issue's
  blockers elsewhere are not seen, so its `unblocked` finding judges only the blockers in the graph.
- **Unchanged:** no dydo command creates, updates, provisions or mirrors Linear objects. Any command
  that would write to Linear still needs its own decision.

---

## Supersedes and amends

Amends [DR 052](./052-dydo-map-read-only-linear-view.md) and, through it,
[DR 044](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md)'s Integration posture: `dydo map`
is no longer the only dydo command that reads Linear. Their ownership and no-mirror rules are
unchanged.

## Affects

- [DR 052 - `dydo map`](./052-dydo-map-read-only-linear-view.md)
- [DR 044 - Linear canonical PM](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md)
- [Architecture Overview](../../understand/architecture.md)
- [Work Model](../../understand/work-model.md)
- [DynaDocs](../../reference/about-dynadocs.md)
- [CLI Commands](../../reference/dydo-commands.md)
