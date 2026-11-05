# Catch-up example

Illustrative, frozen on 30 October 2026. The facts are not current, and its questions need no answer. It shows one Project at two scopes: the admiral's catch-up, then one captain's.

## Admiral scope

- **Covers:** the dydo 3.6.0 Project, three deliverables.
- **State:** One deliverable has landed, one is being rewritten, and the shell-command guard fix is held.
- **Running:** A docs writer is rewriting the catch-up skill.
- **Needs you:** One decision: how to continue the guard fix.

### Shell-command guard: held until you choose

#### Purpose

🎯 **Let agents run harmless searches and file writes without false warnings, while risky commands stay refused.**

The guard checks each shell command an agent runs. An October retro found six kinds of false refusal. For example, `grep "rm -rf" notes.md` was refused as a delete because of the quoted text. You asked for those six fixes, not a parser redesign.

<details>
<summary>📖 Terms</summary>

- **Body text:** text a command writes to a file or feeds to another program.
- **Relief:** allowing a harmless case that the guard used to refuse.

</details>

#### Recently completed

✅ **Review found the fix unsafe to merge: it lets two real commands through.**

To allow harmless body text, the fix treats it as data. But a command can print text that a later shell runs, and Bash can run text that only looks like PowerShell data. In both cases the guard misses a real command.

✅ **The last fix attempt stopped without a result.**

Inside a nested command, the guard loses track of where the text goes. A local check therefore cannot prove that the text stays data.

<details>
<summary>🔎 Evidence</summary>

Eight review rounds failed. The rounds found both misses by feeding inert strings to the hook harness, and ran no payload. The general test suite passed and missed both cases. The code is `Services/BashCommandAnalyzer.cs`.

</details>

#### In flight

⏸️ **Nothing is running. The fix waits on your choice.**

#### Next

❓ **Keep body-text relief through a new design, give it up, or leave the fix held?**

- **Design first.** An agent designs a way to keep all six fixes and stop both misses, with an estimate. A yes authorizes design only, no code. Cost and feasibility are unknown.
- **Give up body-text relief.** The other five fixes stay. Harmless writes whose body text quotes a risky command can be refused again. This needs a scoped fix and a review.
- **Leave it held.** All six false refusals stay, and work that depends on the guard waits.

➡️ **Design first.** It is the only option that keeps the behavior you approved, and it costs one design step before any code changes.

🤖 **After your answer, the admiral starts the chosen step.** Without one, the fix stays held. (DYD-437)

### Catch-up skill: rewrite running

#### Purpose

🎯 **Give you a catch-up that you can understand and decide from without opening links.** An earlier catch-up asked you to approve "option B" and never said what it was.

#### Recently completed

✅ **You set the shape: the story first, four headings per deliverable, a cue on each item.**

#### In flight

✈️ **A docs writer is rewriting the skill and its example. A fresh reviewer judges them next.**

#### Next

💬 **Later: you read the example in a walkthrough and give your verdict on the wording.** (DYD-442)

### Completion guidance: landed

#### Purpose

🎯 **Stop finished work waiting for someone to nudge the admiral.** Overnight, returned work sat idle until a watch fired.

#### Recently completed

✅ **Merged: when work returns, the admiral takes the next step or records why it holds.** (PR 217)

#### In flight

⏸️ **Nothing is running. The guidance is delivered.**

#### Next

🤖 **The admiral applies it at the next real return, which shows whether it works.**

## Captain scope

The catch-up skill's captain tells its own deliverable's story. The crew's dispatches and review rounds stay out; the failed review appears once, as an outcome.

- **Covers:** the catch-up skill, one deliverable of the dydo 3.6.0 Project, which improves the tools agents use to work safely and keep delivery moving.
- **State:** The rewrite is with its writer. Your wording verdict comes after review.
- **Running:** A docs writer is rewriting the skill and its example.
- **Needs you:** Nothing needs you.

### Catch-up skill: rewrite running

#### Purpose

🎯 **Give you a catch-up that you can understand and decide from without opening links.**

A TeasePlayer catch-up asked you to approve "option B" for "creator-code admission" and mentioned a "double cast". It did not say what any of these were or why the work had stopped. To decide, you would have had to rebuild the context you had given the agent.

#### Recently completed

✅ **You set the shape.** The opening tells the story, each deliverable follows under four plain headings, each item starts with a cue, and every choice comes with its options and a recommendation.

✅ **The first rewrite failed review.** Its example still used ticket labels that a reader cannot decode.

#### In flight

✈️ **A docs writer is rewriting the skill and its example.** The example is much shorter and explains each label where it appears.

#### Next

🤖 **A fresh reviewer judges the rewrite against your shape; the captain then returns it to the admiral.**

❗ **The merge waits on a CI fix that the release captain owns.**

💬 **Later: you read the example in a walkthrough and give your verdict on the wording.** Your approval is the last step before the skill is done. (DYD-442)
