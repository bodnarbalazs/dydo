# Check-in

The check-in keeps every lane of the Project moving. A cheap surveyor reads the board and returns
actions; the Admiral acts on them. The bulk reads stay in the surveyor's context.

## Ledger

The `## Lane ledger` section of the Project description, rewritten in place on every check-in; it
holds only the current state. One table row per lane:

| Lane | Order | Owner | State | Next event |
| -- | -- | -- | -- | -- |
| `<name>` | `<tickets in order>` | `<captain task or thread link>` | `<state>` | `<event>` |

State is one of: `moving`, `waiting on <prerequisite>, cleared by <who>`, `admiral owes <decision or
dispatch>`, `needs the user: <Question or gate>`, `deferred by the user`, `done`.

## Schedule

- [ ] Project goes `In Progress`: schedule the check-in every 20 minutes, bound to this thread
      (T3 `schedule_task`, interval). Record its full ID on the Project.
- [ ] You wait on the user's word alone, he takes the map back, or the Project closes: delete the
      schedule, then list schedules and confirm the ID is gone.
- [ ] New work becomes pickable after a deletion: schedule it again.

## Tick — the Admiral

- [ ] A survey is pending: consume it; dispatch no second one.
- [ ] Otherwise dispatch one fresh surveyor on the smallest adequate model with the fixed dispatch below.
- [ ] Carry out every action it returns, in its order.
- [ ] Rewrite the ledger.
- [ ] `NO_ACTION` and no change: end the tick in one line.
- [ ] `INCOMPLETE`: read what it could not, or record the gap on the Project.

## Survey — the surveyor

You are the Admiral's read-only surveyor. Read this section alone; assume no officer role. Send no
messages; edit nothing; start no work, subagents or schedules. Return through the task result only.

- [ ] Run `dydo survey <Project ID>`. Each finding it prints becomes an action.
- [ ] For each ledger lane, read its owner's latest event: task or thread state, last hop SHA, PR
      state. A lane is moving only while its owner runs, or on an event newer than the previous check-in.
- [ ] Flag each lane whose owner ended, died or went quiet with work left.
- [ ] Flag each wait on the user that has no Question.
- [ ] `dydo survey` failed: return `INCOMPLETE` with its error.

Return one of:

- `NO_ACTION`: every item above checked, nothing flagged.
- `ATTENTION`: one line per action, most urgent first:
  `<verb> <who> on <what> — <evidence link, UTC time>`. Verbs: `commission` a captain for a
  ticket, `resume` a captain from its hop SHA, `message` a captain's thread with the question,
  `rewire`, `fix ledger`, `ask the user`.
- `INCOMPLETE`: what could not be read; keep the verified actions.

## Fixed dispatch

```text
Read <absolute path to admiral/resources/check-in.md>; perform only its Survey section.
Project: <Project ID>
Ledger: <pasted from the Project description>
Previous check-in: <UTC time, or none>
Parent return address: <Admiral thread link>; return through this task's result only.
```
