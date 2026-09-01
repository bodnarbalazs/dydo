---
mode: prototype
description: Throwaway code that answers a question. Use when a state model or a piece of logic has to be felt before it is trusted, when what a screen should look like is the open question, or when a question Issue is too abstract to answer in prose.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills prototype at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Prototype

A prototype is **throwaway code that answers a question**. The question decides the shape. Chart
is the stage: `wayfinder` files a prototype question Issue when how something should look or
behave is what the route waits on, and a co-thinker whose exchange has gone abstract routes its
question the same way — through the Project Planner's charting.

## Pick a branch

Identify which question is being answered, using the question Issue, the prompt, the surrounding
code, or by asking if the human is around:

- **"Does this logic / state model feel right?"** → a **logic** prototype: one shareable HTML
  file a non-developer can drive — free-play buttons plus guided walkthroughs. Build it by
  [logic](resources/logic.md).
- **"What should this look like?"** → a **UI** prototype: several radically different variations
  on one route, switchable behind a `?variant=` param. Build it by [ui](resources/ui.md).

The two branches produce very different artifacts, so getting this wrong wastes the whole prototype.
If the question is genuinely ambiguous and the human isn't reachable, default to whichever branch
better matches the surrounding code (a backend module → logic; a page or component → UI) and state
the assumption at the top of the prototype.

## Rules that apply to both

1. **Throwaway from day one, and clearly marked as such.** Locate the prototype code close to
   where it will actually be used (next to the module or page it's prototyping for) so context is
   obvious, but name it so a casual reader can see it's a prototype, not production. For throwaway
   UI routes, obey whatever routing convention the project already uses; don't invent a new
   top-level structure.
2. **Trivial to run.** A UI prototype starts from one command in the project's task runner:
   `pnpm <name>`, `python <path>`, `bun <path>`, etc. A logic demo is a single HTML file the human
   double-clicks. Either way, no thinking required to start it.
3. **No persistence by default.** State lives in memory. Persistence is the thing the prototype is
   _checking_, not something it should depend on. If the question explicitly involves a database,
   hit a scratch DB or a local file with a clear "PROTOTYPE, wipe me" name.
4. **Skip the polish.** No tests, no error handling beyond what makes the prototype _runnable_, no
   abstractions. The point is to learn something fast.
5. **Surface the state.** After every action (logic) or on every variant switch (UI), print or
   render the full relevant state so the human can see what changed.
6. **Capture it when done.** Fold what the prototype proved into the real code, then capture the
   prototype itself as a **primary source**: commit it to a `prototype/<name>` branch, out of
   main, and link that branch from the question Issue it answers. Capture the answer too (the
   verdict and the question it settled) on that Issue. The main branch keeps only what was folded
   in. The branch never merges.
