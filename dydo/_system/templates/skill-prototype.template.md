---
mode: prototype
description: Throwaway code that answers a question. Use when a state model or a piece of logic has to be felt before it is trusted, when what a screen should look like is the open question, or when a question Issue is too abstract to answer in prose.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills prototype at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Prototype

A prototype is **throwaway code that answers a question**. The question decides the shape. Chart
is the stage, where it raises the fidelity of an answer prose cannot settle: wayfinder files a
prototype question Issue when how something should look or how it should behave is the question
the route waits on. A co-thinker whose exchange with the human has gone abstract sends the
question the same way: the planner charts the Project with wayfinder, and this prototype question
Issue is one of the map's.

## Pick a branch

Identify which question is being answered, using the question Issue, the prompt, the surrounding
code, or by asking if the human is around:

- **"Does this logic / state model feel right?"** → a **logic** prototype: build a single
  shareable HTML file (free-play buttons plus tabbed guided walkthroughs) that pushes the state
  machine through cases that are hard to reason about on paper, and that a non-developer can
  drive.
- **"What should this look like?"** → a **UI** prototype: generate several radically different UI
  variations on a single route, switchable via a URL search param and a floating bottom bar.

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

## Logic Prototype

A single, self-contained HTML file (a **shareable demo**) that lets anyone drive a state model by
clicking buttons. Use this when the question is about **business logic, state transitions, or data
shape**: the kind of thing that looks reasonable on paper but only feels wrong once you push it
through real cases.

Because it's one file with nothing to install, you can hand it to a non-developer (a designer, a PM,
a domain expert) and let them feel the model for themselves. So it speaks their language, not the
code's.

### When this is the right shape

- "I'm not sure if this state machine handles the edge case where X then Y."
- "Does this data model actually let me represent the case where..."
- "I want to feel out what the API should look like before writing it."
- Anything where someone wants to **press buttons and watch state change**.

### 1. State the question

Before writing code, write down what state model and what question you're prototyping. One
paragraph, at the top of the demo (in a visible intro, not just a comment). A logic prototype that
answers the wrong question is pure waste, so make the question explicit so it can be checked
later, whether the human is watching now or returning to it AFK.

### 2. Isolate the logic in a portable module

Put the actual logic (the bit that's answering the question) in a single `<script>` block written as
a small, pure module that could be lifted out and dropped into the real codebase later. The page
around it is throwaway; this module isn't.

The right shape depends on the question:

- **A pure reducer**: `(state, action) => state`. Good when actions are discrete events and state is
  a single value.
- **A state machine**: explicit states and transitions. Good when "which actions are even legal
  right now" is part of the question.
- **A small set of pure functions** over a plain data type. Good when there's no implicit current
  state, just transformations.
- **A class or module with a clear method surface** when the logic genuinely owns ongoing internal
  state.

Pick whichever shape best fits the question being asked, *not* whichever is easiest to wire to a
page. Keep it pure: no DOM, no `document`, no button handlers reaching inside it. The page calls
into it; nothing flows the other direction. This is what makes the prototype useful past its own
lifetime: once the question's answered, the validated reducer / machine / function set lifts into
the real module on its own.

### 3. Build the shareable HTML file

One file, plain HTML/CSS/JS: no framework, no bundler, no server, everything inline so it opens by
double-click and survives being emailed around. Anyone should be able to run it by opening it.

Write it for a non-developer. Every label is in **domain language**, not code: buttons and state
read like the business, not the reducer. Explain in plain words what's happening.

Lay it out with a clean hierarchy, top to bottom:

1. **Title and one-line explanation** of what this demo lets you explore (the question from step 1).
2. **Current state**: the full relevant state, rendered as a readable panel (labelled fields, not a
   raw JSON dump), re-rendered after every click so the change is visible. Where it helps a
   non-developer follow, call out what just changed.
3. **Free-play buttons**: one button per action, always available, so anyone can poke at the model
   in any order. Each click dispatches its action and re-renders the state.
4. **Guided walkthroughs**: a set of **scenarios**, one per tab. Each tab holds a short
   plain-language description of the scenario (the situation it sets up and what to watch for) and
   underneath it, the ordered **buttons to press** for that scenario. Each step is a real button:
   clicking it performs that action and moves to the next step. Starting a walkthrough resets to a
   known initial state so the scenario runs the same way every time.

Choose scenarios that demonstrate the awkward cases, the ones hard to reason about on paper: the
happy path, a tricky edge case, an attempt at something that should be illegal.

Keep it beautiful but restrained: clean typography, generous spacing, one accent colour. No
animations, no gimmicks: nothing that competes with the state and the buttons.

### 4. Hand it over

Send them the file, or open it for them. They'll click through the walkthroughs and free-play
whenever they get to it; the interesting moments are when they say "wait, that shouldn't be
possible" or "huh, I assumed X would be different"; those are the bugs in the _idea_, which is the
whole point. If they want new actions or a new scenario, add them. Prototypes evolve.

### 5. Capture the answer and the prototype

Once the prototype has answered its question, capture the answer, then capture the prototype the way
rule 6 describes. The logic-specific mapping: the validated reducer / machine / function set lifts
into the real module (the answer, absorbed); the HTML shell rides along to the `prototype/<name>`
branch that keeps the prototype as a primary source, and being one self-contained file, it stays
trivially re-runnable there.

### Anti-patterns

- **Don't generalise.** No "what if we wanted to support X later." The prototype answers one
  question.
- **Don't blur the logic and the page together.** If the pure module references the DOM, `document`,
  or button handlers, it's no longer liftable. Keep the page as a thin shell over a pure module.
- **Don't reach for a framework, bundler, or server.** One file the recipient double-clicks; a React
  app or a dev server defeats "shareable".
- **Don't ship the HTML shell into production.** The page is optimised for being clicked through by
  hand. The logic module behind it is the bit worth keeping.

## UI Prototype

Generate **several radically different UI variations** on a single route, switchable from a floating
bottom bar. The human flips between variants in the browser, picks one (or steals bits from each),
then throws the rest away.

### When this is the right shape

- "What should this page look like?"
- "I want to see a few options for this dashboard before committing."
- "Try a different layout for the settings screen."
- Any time the human would otherwise spend a day picking between three vague mockups in their head.

### Two sub-shapes: strongly prefer sub-shape A

A UI prototype is much easier to judge when it's **butting up against the rest of the app**: real
header, real sidebar, real data, real density. A throwaway route on its own is a vacuum: every
variant looks fine in isolation. Default to sub-shape A whenever there's a plausible existing page
to host the variants. Only reach for sub-shape B if the prototype genuinely has no nearby home.

**Sub-shape A: adjustment to an existing page (preferred).** The route already exists. Variants are
rendered **on the same route**, gated by a `?variant=` URL search param. The existing data fetching,
params, and auth all stay. Only the rendering swaps. This is the default; pick it unless there's a
specific reason not to. If the prototype is for something that doesn't yet have a page but *would
naturally live inside one* (a new section of the dashboard, a new card on the settings screen, a new
step in an existing flow), it's still sub-shape A. Mount the variants inside the host page.

**Sub-shape B: a new page (last resort).** Only use this when the thing being prototyped genuinely
has no existing page to live inside (e.g. an entirely new top-level surface, or a flow that can't be
embedded anywhere sensible). Create a **throwaway route** following whatever routing convention the
project already uses. Don't invent a new top-level structure. Name it so it's obviously a prototype
(e.g. include the word `prototype` in the path or filename). Same `?variant=` pattern. Before
committing to sub-shape B, sanity-check: is there really no existing page this could be embedded in?
An empty route hides design problems that a populated one would expose.

In both sub-shapes the floating bottom bar is identical.

### 1. State the question and pick N

Default to **3 variants**. More than 5 stops being radically different and starts being noise, so
cap there.

Write down the plan in one line, in the prototype's location or a top-of-file comment:

> "Three variants of the settings page, switchable via `?variant=`, on the existing `/settings`
> route."

This works whether the human is here to push back or not.

### 2. Generate radically different variants

Draft each variant. Hold each one to:

- The page's purpose and the data it has access to.
- The project's component library / styling system (TailwindCSS, shadcn, MUI, plain CSS, whatever).
- A clear exported component name, e.g. `VariantA`, `VariantB`, `VariantC`.

Variants must be **structurally different**: different layout, different information hierarchy,
different primary affordance, not just different colours. Three slightly-tweaked card grids isn't a
UI prototype, it's wallpaper. If two drafts come out too similar, redo one with explicit "do not use
a card grid" guidance.

### 3. Wire them together

Create a single switcher component on the route:

```tsx
// pseudo-code, adapt to the project's framework
const variant = searchParams.get('variant') ?? 'A';
return (
  <>
    {variant === 'A' && <VariantA {...data} />}
    {variant === 'B' && <VariantB {...data} />}
    {variant === 'C' && <VariantC {...data} />}
    <PrototypeSwitcher variants={['A','B','C']} current={variant} />
  </>
);
```

For sub-shape A (existing page): keep all the existing data fetching above the switcher; only the
rendered subtree changes per variant.

For sub-shape B (new page): the throwaway route under `/prototype/<name>` mounts the same switcher.

### 4. Build the floating switcher

A small fixed-position bar at the bottom-centre of the screen with three pieces:

- **Left arrow**: cycles to the previous variant (wraps around).
- **Variant label**: shows the current variant key and, if the variant exports a name, that name
  too. e.g. `B (Sidebar layout)`.
- **Right arrow**: cycles forward (wraps around).

Behaviour:

- Clicking an arrow updates the URL search param (use the framework's router, e.g. `router.replace`
  on Next, `navigate` on React Router, etc) so the variant is shareable and reload-stable.
- Keyboard: `←` and `→` arrow keys also cycle. Don't intercept arrow keys when an `<input>`,
  `<textarea>`, or `[contenteditable]` is focused.
- Visually distinct from the page (e.g. high-contrast pill, subtle shadow) so it's obviously not
  part of the design being evaluated.
- Hidden in production builds: gate on `process.env.NODE_ENV !== 'production'` or an equivalent
  check, so a stray prototype merge can't ship the bar.

Put the switcher in a single shared component so both sub-shapes can reuse it. Locate it wherever
shared UI lives in the project.

### 5. Hand it over

Surface the URL (and the `?variant=` keys). The human will flip through whenever they get to it. The
interesting feedback is usually **"I want the header from B with the sidebar from C"**, which is the
actual design they want.

### 6. Capture the answer and clean up

Once a variant has won, capture the answer (which variant and why), then capture the prototype the
way rule 6 describes. Fold the winner into the real code and move the rest onto the
`prototype/<name>` branch, not into main:

- **Sub-shape A**: fold the winner into the existing page; drop the losing variants and the switcher
  from main.
- **Sub-shape B**: promote the winning variant to a real route; drop the throwaway route and the
  switcher from main.

The full set of variants is the primary source, so it lands on the branch, not the bin, since
variant components and the switcher left in the main branch rot fast and confuse the next reader.

### Anti-patterns

- **Variants that differ only in colour or copy.** That's a tweak, not a prototype. Real variants
  disagree about structure.
- **Sharing too much code between variants.** A shared `<Header>` is fine; a shared `<Layout>`
  defeats the point. Each variant should be free to throw out the layout.
- **Wiring variants to real mutations.** Read-only prototypes are fine. If a variant needs to
  mutate, point it at a stub: the question is "what should this look like", not "does the backend
  work".
- **Promoting the prototype directly to production.** The variant code was written under prototype
  constraints (no tests, minimal error handling). Rewrite it properly when you fold it in.
