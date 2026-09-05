---
name: prototype
description: Build a throwaway prototype to answer a design question. Use when the human wants to sanity-check whether a state model or logic feels right, or explore what a UI should look like.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills prototype at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Prototype

A prototype is **throwaway code that answers a question**. The question decides the shape.

## Pick a branch

Identify which question is being answered, using the Prototype Issue, the prompt and surrounding
code. A worker returns a missing choice to the Captain, whose HITL session reaches the human:

- **"Does this logic / state model feel right?"** → [logic](resources/logic.md). Build a single
  shareable HTML file (free-play buttons plus tabbed guided walkthroughs) that pushes the state
  machine through cases that are hard to reason about on paper, and that a non-developer can drive.
- **"What should this look like?"** → [ui](resources/ui.md). Generate several radically different
  UI variations on a single route, switchable via a URL search param and a floating bottom bar.

The two branches produce very different artifacts, so getting this wrong wastes the whole prototype.
If the question is ambiguous, return the missing choice and available evidence to the Captain;
their HITL session resolves it with the human before the prototype proceeds.

## Rules that apply to both

1. **Throwaway from day one, and clearly marked as such.** Work on a `prototype/<name>` branch in
   its own worktree, where production standards do not apply. Locate the prototype code close to
   where it will actually be used (next to the module or page it's prototyping for) so context is
   obvious, but name it so a casual reader can see it's a prototype, not production. For throwaway
   UI routes, obey whatever routing convention the project already uses; don't invent a new
   top-level structure.
2. **Trivial to run.** A UI prototype starts from one command in the project's task runner:
   `pnpm <name>`. A logic demo is a single HTML file the human double-clicks. Either way, no
   thinking required to start it.
3. **No persistence by default.** State lives in memory. Persistence is the thing the prototype is
   _checking_, not something it should depend on. If the question explicitly involves a database,
   hit a scratch DB or a local file with a clear "PROTOTYPE, wipe me" name.
4. **Skip the polish.** No tests, no error handling beyond what makes the prototype _runnable_, no
   abstractions. The point is to learn something fast.
5. **Surface the state.** After every action (logic) or on every variant switch (UI), print or
   render the full relevant state so the human can see what changed.
6. **Capture it when done.** Record the human's verdict and the winning commit on the Prototype
   Issue. The `prototype/<name>` branch is a **primary source**, linked and never submitted or
   merged. A delivery Issue's specifier reads it as input, never as a base; retain it until that
   delivery Issue is `Done`, or until feature cleanup. The delivery crew implements the decision.
