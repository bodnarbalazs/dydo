# Check-in

The check-in keeps every lane of the Project moving. A cheap surveyor reads the board and returns
actions; the Admiral acts on them. The bulk reads stay in the surveyor's context.

## Ledger

The `## Lane ledger — <UTC time>` section of the Project description, rewritten in place whenever
it changes; it holds only the current state, and its time is that change. One table row per lane,
then the `Filler` row:

| Lane | Order | Owner | State | Next action |
| -- | -- | -- | -- | -- |
| `<name>` | `<issue keys in order>` | `<captain thread link>` | `<state>` | `<who does what, or the event awaited>` |
| Filler | `<issue keys, next first>` | — | `ready` | taken when a lane slot is free |

State is one of: `moving`, `waiting on <prerequisite>, cleared by <who>`, `blocker-resolution
running: <who>`, `admiral owes <decision or dispatch>`, `needs the user: <Question or gate>`,
`deferred by the user`, `done`.

## Schedule

- [ ] Project goes `In Progress`, or you take over an active one: schedule the check-in every 20
      minutes, bound to this thread (T3 `schedule_task`, interval), with the prompt
      `Admiral check-in for <Project>: read <absolute path to this file> and run its Tick section.`
      Record its full ID on the Project.
- [ ] On each user wake: list schedules; the recorded ID missing or disabled, schedule it again.
- [ ] You wait on the user's word alone, he takes the map back, or the Project closes: delete the
      schedule, then list schedules and confirm the ID is gone.
- [ ] New work becomes pickable after a deletion: schedule it again.

## Tick — the Admiral

- [ ] Handle every delivered return and receipt first, by Method step 1.
- [ ] You relay a new rule from the user: send it to every lane owner in the ledger, not only those
      you think it affects.
- [ ] A survey is still running: consume it; dispatch no second one. A failed or lost survey is not running.
- [ ] Otherwise dispatch one fresh surveyor on the smallest adequate model with the fixed dispatch below.
- [ ] Carry out every action it returns, in its order.
- [ ] Keep at most the Project's lane cap moving, four unless the user set another. Main lanes take
      slots first; while a main lane only waits (on a prerequisite or the user) or nothing main is
      pickable, fill each free slot with the next `Filler` ticket.
- [ ] `INCOMPLETE`: read what it could not, or record the gap on the Project.
- [ ] Rewrite the ledger if anything changed; otherwise end the tick in one line.

## Survey — the surveyor

You are the Admiral's read-only surveyor. Read this section alone; assume no officer role. Send no
messages; edit nothing; start no work, subagents or schedules. Return through the task result only.

- [ ] Run `dydo survey <Project ID>`.
- [ ] Turn each finding into its action: `pickable` and `unblocked` → `commission`; `uncontracted`
      → `contract` (to `Todo`, then commission); any of these on a `Filler` ticket → `fill`;
      `unlaned` and `stale-row` → `fix ledger`; `started-unassigned` → `message` or `resume` its
      captain; `question-no-priority` → `prioritize`.
- [ ] For each ledger lane, read its owner's thread (`t3_thread_read`) and its ticket's record: last
      hop SHA, PR state, and the crew the captain dispatched and whether each returned.
- [ ] A captain's thread shows a finished result the ledger doesn't reflect: return it as
      `handle <captain> return — <its result line, UTC time>`. Reading it marked it delivered, so
      the admiral sees it only through you.
- [ ] Flag a lane as stalled when its owner or crew ended with work left and no return delivered, or
      when nothing happened since the ledger's time while the owner neither runs, waits on its crew,
      nor waits on its recorded prerequisite.
- [ ] Flag each `needs the user` lane that names no Question or recorded gate (approval, landing
      click, walkthrough).
- [ ] `dydo survey` failed: return `INCOMPLETE` with its error.

Return one of:

- `NO_ACTION`: every item above checked, nothing flagged.
- `ATTENTION`: one line per action, most urgent first:
  `<verb> <who> on <what> — <evidence link, UTC time>`. Verbs: `commission` a captain for a
  ticket, `contract` a Backlog ticket, `fill` a free slot, `resume` a captain from its hop SHA,
  `message` a captain's thread with the question, `handle` a captain's return you saw, `rewire`, `prioritize`, `fix ledger`, `ask the user`.
- `INCOMPLETE`: what could not be read; keep the verified actions.

## Fixed dispatch

```text
Read <absolute path to admiral/resources/check-in.md>; perform only its Survey section.
Project: <Project ID>
Ledger: <pasted from the Project description>
Parent return address: <Admiral thread link>; return through this task's result only.
```
