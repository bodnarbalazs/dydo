---
mode: codebase-design
description: Deep modules — the vocabulary and principles for designing one. Use when shaping a module's interface, weighing depth against the leverage it buys, choosing the seam a test will cross, or judging a design under review.
emit: skill
invocation: automatic
---

<!-- Adapted from mattpocock/skills codebase-design at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Codebase Design

Design **deep modules**: a lot of behaviour behind a small interface, placed at a clean seam, testable
through that interface. Use this language and these principles wherever code is designed or
restructured. The aim is leverage for callers, locality for maintainers, and testability for everyone.
Planning reaches for it most — the planner for a plan's architecture-level design, the reviewer when it
judges a design, the test-writer when it picks the seam a test crosses.

## Glossary

Use these terms exactly: don't substitute "component," "service," "API," or "boundary." Consistent
language is the whole point.

**Module**: anything with an interface and an implementation. Deliberately scale-agnostic: a function, a
class, a package, or one path across the tiers. _Avoid_: unit, component, service.

**Interface**: everything a caller must know to use the module correctly: the type signature, but also
invariants, ordering constraints, error modes, required configuration, and performance characteristics.
_Avoid_: API, signature (too narrow, they refer only to the type-level surface).

**Implementation**: what's inside a module, its body of code. Distinct from **Adapter**: a thing can be a
small adapter with a large implementation (a Postgres repository) or a large adapter with a small one
(an in-memory fake). Reach for "adapter" when the seam is the topic; "implementation" otherwise.

**Depth**: leverage at the interface. The behaviour a caller (or a test) can exercise per unit of
interface it has to learn. A module is **deep** when a large amount of behaviour sits behind a small
interface, **shallow** when the interface is nearly as complex as the implementation it passes calls
through to. Shaping one, ask: fewer methods, simpler parameters, more complexity hidden inside? Depth is
not Ousterhout's ratio of implementation lines to interface lines, which rewards padding the body.

**Seam** _(Michael Feathers)_: a place where you can alter behaviour without editing in that place; the
_location_ at which a module's interface lives. Where to put the seam is its own design decision,
distinct from what goes behind it. _Avoid_: boundary (overloaded with DDD's bounded context).

**Adapter**: a concrete thing that satisfies an interface at a seam. Describes _role_ (what slot it
fills), not substance (what's inside).

**Leverage**: what callers get from depth. More capability per unit of interface they learn; one
implementation pays back across N call sites and M tests.

**Locality**: what maintainers get from depth. Change, bugs, knowledge and verification concentrate in
one place rather than spreading across callers. Fix once, fixed everywhere.

## Principles

- **Depth is a property of the interface, not the implementation.** A deep module can be composed
  internally of small, swappable parts; they just aren't part of the interface. It can hold **internal
  seams** (private to the implementation, used by its own tests) as well as the **external seam** at its
  interface; an internal seam stays internal even when a test uses it.
- **The deletion test.** Imagine deleting the module. If complexity vanishes, it was a pass-through. If
  complexity reappears across N callers, it was earning its keep.
- **The interface is the test surface.** Callers and tests cross the same seam. If you want to test
  _past_ the interface, the module is probably the wrong shape.
- **One adapter means a hypothetical seam. Two adapters means a real one.** Put a seam where something
  actually varies across it — typically production plus test. A single-adapter seam is indirection.

## Designing for testability

Good interfaces make testing natural.

- **Accept dependencies, don't create them.** A module handed its payment gateway can be exercised; one
  that builds a live gateway inside itself can't.
- **Return results, don't produce side effects.** Returning the discount is testable; mutating the
  cart's total in place is not.
- **Small surface area.** Fewer methods, fewer tests. Fewer parameters, simpler test setup.

**Dependencies decide the seam.** Classify them before deepening a module; the category settles how the
deepened module is tested across its seam.

- **In-process** — pure computation, in-memory state, no I/O. Always deepenable: merge the modules and
  test through the new interface directly. No adapter needed.
- **Local-substitutable** — a local stand-in exists (PGLite for Postgres, an in-memory filesystem).
  Deepenable while it does; the stand-in runs in the test suite and the seam stays internal, off the
  module's external interface.
- **Remote but owned** — your own services across a network. Define a port at the seam: the deep module
  owns the logic, the transport is injected as an adapter — HTTP in production, in-memory in tests.
- **True external** — a third party you don't control. The module takes it as an injected port; tests
  supply a mock adapter.

**Replace, don't layer.** Once tests exist at the deepened interface, the old tests on the shallow parts
are waste: delete them. New tests assert observable outcomes through the interface, so they survive
internal refactors — one that must change when the implementation changes was testing past the interface.

## Design it twice

Your first interface is unlikely to be the best (Ousterhout). When the design is load-bearing, produce
three radically different interfaces before choosing, one per constraint: minimise the interface (one to
three entry points, maximum leverage each); maximise flexibility and extension; optimise for the most
common caller, making the default case trivial. Add a fourth around ports and adapters when a dependency
crosses a seam. The designs are disjoint, so spawn one sub-agent per constraint when you can, briefing
each with the file paths, the dependency category, and what sits behind the seam. Each returns its
interface (types, invariants, ordering, error modes), a usage example, what it hides, its adapters, and
where its leverage is thin. Compare on depth, locality and seam placement, then recommend one — or a
hybrid, and say which parts. An opinionated read, not a menu.
