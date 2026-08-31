---
mode: improve-codebase-architecture
description: Hunt the codebase for deepening opportunities, see them in a visual report, then grill whichever one you pick.
emit: skill
invocation: explicit
---

<!-- Adapted from mattpocock/skills improve-codebase-architecture at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Improve Codebase Architecture

Surface architectural friction and propose **deepening opportunities**: refactors that turn shallow
modules into deep ones. The aim is testability and AI-navigability.

This command is _informed_ by the project's domain model and built on a shared design vocabulary:

- Call the Skill tool with `codebase-design` for the architecture vocabulary (**module**,
  **interface**, **depth**, **seam**, **adapter**, **leverage**, **locality**) and its principles
  (the deletion test, "the interface is the test surface", "one adapter = hypothetical seam, two =
  real"). Use these terms exactly in every suggestion, and don't drift into "component," "service,"
  "API," or "boundary."
- The domain language in `dydo/glossary.md` gives names to good seams; Decision Records record
  choices this command should not re-litigate.

## Process

### 1. Explore

**Scope before you scan: YAGNI.** Deepening a module pays off by making future changes to it easier,
so put extra weight on the parts of the codebase that have recently changed. Decide *where* to look
before you look:

- If the human named a direction (a module, a subsystem, a pain point), take it, and skip the
  inference below.
- Otherwise, walk back a good stretch of the commit history (`git log --oneline`) to find the
  codebase's hot spots, the files and areas that keep coming up, and let those paths pull your
  attention first. If the changes are scattered with no clear hot spot, widen the net.

Read `dydo/glossary.md` and the Decision Records covering the area you're touching first.

Then spawn a sub-agent to walk the codebase. Don't follow rigid heuristics; explore organically and
note where you experience friction:

- Where does understanding one concept require bouncing between many small modules?
- Where are modules **shallow**, with an interface nearly as complex as the implementation?
- Where have pure functions been extracted just for testability, but the real bugs hide in how they
  are called (no **locality**)?
- Where do tightly-coupled modules leak across their seams?
- Which parts of the codebase are untested, or hard to test through their current interface?

Apply the **deletion test** to anything you suspect is shallow: would deleting it concentrate
complexity, or just move it? A "yes, concentrates" is the signal you want.

### 2. Present candidates as an HTML report

Write a self-contained HTML file into a scratch directory outside the repository, so nothing lands in
the repo and nothing is ever committed. Give each run its own
`architecture-review-<timestamp>.html`, open it for the human, and tell them the absolute path.

The report uses **Tailwind via CDN** for layout and styling and **Mermaid via CDN** for diagrams.
Those two are its only scripts; it is otherwise static. Each candidate gets a **before/after
visualisation**. Be visual: the diagrams carry the weight and the prose stays sparse, so if a diagram
needs a paragraph to be understood, redraw the diagram.

For each candidate, render a card with:

- **Files**: which files and modules are involved, monospaced.
- **Problem**: one sentence. What hurts.
- **Solution**: one sentence, plain English. What changes.
- **Wins**: bullets of six words or fewer, named in `codebase-design` terms — "locality: bugs
  concentrate in one module", "interface shrinks; implementation absorbs the wrappers", "tests hit
  one interface" — and how tests would improve.
- **Before / After diagram**: the centrepiece, side by side, illustrating the shallowness and the
  deepening.
- **Recommendation strength**: one of `Strong`, `Worth exploring`, `Speculative`, as a badge.

Pick the diagram pattern that fits the candidate and vary it: a Mermaid flowchart or sequence when
the relationship is graph-shaped (call graphs, dependencies, "before: 6 round-trips; after: 1"),
hand-built divs and inline SVG when Mermaid's layout fights you or the "after" needs one
thick-bordered deep module, a cross-section of stacked bands for layered shallowness, a mass diagram
of interface against implementation, a call-graph collapse for calls that become internal. Lean
editorial rather than corporate dashboard: colour sparingly, one accent plus red for leakage and
amber for warnings, diagrams short enough that before and after sit side by side without scrolling.

End the report with a **Top recommendation** section: which candidate you'd tackle first and why.

**Use `dydo/glossary.md` vocabulary for the domain, and the `codebase-design` vocabulary for the
architecture.** If the glossary defines "Order," talk about "the Order intake module," not "the
FooBarHandler," and not "the Order service."

**Decision Record conflicts**: if a candidate contradicts a Decision Record, surface it only when the
friction is real enough to warrant reopening that decision, and mark it in the card (a warning
callout: _"contradicts DR 021, but worth reopening because…"_). Don't list every theoretical refactor
a Decision Record forbids.

Do NOT propose interfaces yet. After the file is written, ask the human: "Which of these would you
like to explore?"

### 3. Grill the candidate the human picks

Once the human picks a candidate, call the Skill tool with `grilling` and walk the decision tree with
them: constraints, dependencies, the shape of the deepened module, what sits behind the seam, what
tests survive. Nothing is planned before the candidate has been through this.

Side effects happen inline as decisions crystallize:

- **Naming a deepened module after a concept not in `dydo/glossary.md`?** Add the term there.
- **Sharpening a fuzzy term during the conversation?** Update `dydo/glossary.md` right there.
- **Want to explore alternative interfaces for the deepened module?** Call the Skill tool with
  `codebase-design` and use its design-it-twice parallel sub-agent pattern.

### 4. Hand over what survived

The whole command sits at the Think stage: the human calls it, it proposes and stress-tests, and it
neither plans nor writes code. Call the Skill tool with `co-thinker` and hand it the surviving
deepening — the seam it moves, what the grilling settled, what is still open, and any candidate the
human rejected with a load-bearing reason. The co-thinker decides where it goes next: ripe enough
for a plan, or a decision durable enough to become a DR.
