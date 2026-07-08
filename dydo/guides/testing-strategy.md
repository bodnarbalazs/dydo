---
area: guides
type: guide
---

# Testing Strategy — Three-Tier System

A tiered testing philosophy that defines levels of rigor based on the criticality of the code being tested. Every source module is checked against its tier's thresholds.

---

## The Three Tiers

### T1 — Baseline

**Everything is T1 by default.** No annotation needed.

- **Tests:** Every non-trivial source module must have at least one corresponding test file
- **Coverage:** ≥ 80% line coverage, ≥ 60% branch coverage
- **Character:** "Does it work correctly under normal use?"
- **What to test:** Happy paths, common error states, basic input validation
- **Examples:** Utility services, standard command handlers, configuration parsing

### T2 — Thorough

For important code that handles significant business logic or user-facing workflows.

- **Coverage:** 100% line coverage, ≥ 80% branch coverage, edge cases systematically covered
- **Character:** "Does it hold up under pressure?"
- **What to test:** Boundary conditions, error states, concurrent scenarios, complex state transitions, all error paths
- **Examples:** Guard enforcement, agent state management, dispatch orchestration

No `coverage:ignore` escape hatches. If code is unreachable, delete it. If a guard triggers rarely, that's exactly what T2 testing should catch.

### T3 — Hardened

For mission-critical code where failure has severe consequences. Applied sparingly — expect a handful of classes total.

- **Coverage:** 100% line coverage, 100% branch coverage, adversarial testing
- **Character:** "Can it be broken?"
- **What to test:** Everything in T2, plus: security tests (injection, malicious input), fuzzing, abuse scenarios
- **Examples:** Hook enforcement (PreToolUse guard), permission validation, audit integrity

---

## Assigning Tiers

T1 is the default. Only T2 and T3 need explicit marking via a comment annotation in the first 10 lines of the **test file**:

```csharp
// @test-tier: 2
```

The test-file marker is authoritative. A T2 component might have some T1 utility tests alongside T2 edge-case tests — the file-level marker reflects what standard *that specific test file* is held to.

---

## CRAP Score Thresholds

Each tier has a **CRAP score** target — a single metric combining cyclomatic complexity (CC) and code coverage. Formula: `CRAP = CC² × (1 - cov)³ + CC`.

| Tier | CRAP ≤ | What it takes |
|------|--------|---------------|
| **T1** | 30 | Test it or keep it simple. At 80% line coverage, CC up to ~25 passes. |
| **T2** | 15 | Real testing investment. CC = 10 needs ~80% coverage. |
| **T3** | 5 | Forced decomposition. Even at 100% coverage, max CC = 4. |

Key properties of CRAP:
- At 100% coverage, CRAP equals CC — a pure complexity measure.
- At 0% coverage, CRAP = CC² + CC — untested complex code is severely penalized.
- The metric rewards either reducing complexity or increasing coverage (ideally both).

CRAP uses the **per-method max** cyclomatic complexity, not the class-level sum. See [Decision 009](../project/decisions/009-crap-per-method-metric.md) for why.

Auto-generated code (e.g., source generators, `obj/` artifacts) is excluded.

### Diagnosing a CRAP failure

Because the gate is CRAP — not a plain line-% threshold — **adding a few guard branches to a T1 file can tip it red even when every test passes**. At CC = 30, full coverage puts CRAP at exactly 30 (pass); 93% coverage puts it at 30.3 (fail). Conversely, adding tests that only re-cover already-covered lines does nothing.

Diagnose with:

```bash
python DynaDocs.Tests/coverage/gap_check.py --inspect <ModuleName>
```

It prints `lines: x/y`, `branches: %`, `CRAP`, `CC`, the uncovered line numbers, and partial branches.

Two levers to get back under the threshold, best used together:

1. **Cut complexity** — e.g. collapse a dead disjunct like `loaded == null || (models = loaded?.Models) == null` into `if (loaded?.Models is not { } models)`; that removes a branch *and* null-narrows `loaded`. Each −1 CC gives real headroom.
2. **Cover the error paths** — not-in-project, corrupt-input, and missing-section branches are the usual uncovered culprits in a service.

---

## Tier Summary

| Metric               | T1                       | T2               | T3                           |
| -------------------- | ------------------------ | ---------------- | ---------------------------- |
| **Line coverage**    | ≥ 80%                    | 100%             | 100%                         |
| **Branch coverage**  | ≥ 60%                    | ≥ 80%            | 100%                         |
| **CRAP score**       | ≤ 30                     | ≤ 15             | ≤ 5                          |
| **Edge cases**       | Key ones                 | Systematic       | Exhaustive + adversarial     |
| **Security testing** | —                        | Input validation | Injection, escaping, fuzzing |

### What counts as "non-trivial"

Excluded from the "has tests" requirement:
- Auto-generated code (source generators, EF migrations, `*.g.cs`)
- Pure data models / record types with no logic (≤ 3 executable lines)
- Program.cs entry point

Everything else — services, command handlers, validators, utilities — needs a test file.

---

## Tooling

`gap_check.py` in `DynaDocs.Tests/coverage/` enforces these tiers:

```bash
python DynaDocs.Tests/coverage/gap_check.py                    # auto-detect: skip or run tests
python DynaDocs.Tests/coverage/gap_check.py --force-run        # always run tests
python DynaDocs.Tests/coverage/gap_check.py --detail           # show uncovered lines
python DynaDocs.Tests/coverage/gap_check.py --inspect Guard    # inspect matching modules
```

See [Coverage Tools](../reference/coverage-tools.md) for full usage reference.

### Enforcement status

Tier thresholds are enforced by agents during code review (via `gap_check.py`). CI enforcement will be added once the test suite matures.

---

## Cross-Platform CI: Windows-Only Test Traps

CI (`.github/workflows/ci.yml`) runs `dotnet test` on **ubuntu-latest as a non-root user**, while development happens on Windows — so Windows-only test assumptions pass locally and forever redden CI. When master CI is red but local Windows tests pass, suspect **platform-specific test bugs**, not whatever commit the timeline happens to blame: the same failures are usually red on the prior commit too, with product code correct and cross-platform.

Two recurring traps:

1. **File locking** — holding a `FileStream(..., FileShare.None)` handle to force `File.Delete` to throw only blocks deletion on Windows; POSIX lets you unlink an open file, so the test fails with "No exception was thrown" on Linux. On Unix, unlink permission is governed by the **parent directory** — induce a delete failure by making the parent dir non-writable (`File.SetUnixFileMode`), not by locking the file. Use the cross-platform helper `DynaDocs.Tests/UndeletableFile.cs`.
2. **Hardcoded `C:\...` path literals** — on Linux `\` is an ordinary filename character, so `Path.GetFileName(@"C:\x\dir")` returns the whole string. Use forward-slash literals; they parse identically on both OSes.

Also: guard Unix-only calls (`File.SetUnixFileMode`) with `OperatingSystem.IsWindows()`, not just a nullness check — the CA1416 analyzer can't correlate `_field != null` with platform, and `TreatWarningsAsErrors=true` (set in `Directory.Build.props`) turns that warning into a CI build break.

### Verifying a Linux fix from Windows

Without burning CI cycles: `git archive HEAD` → overlay your dirty files → run inside `mcr.microsoft.com/dotnet/sdk:10.0` as a **non-root** user (`useradd`, `chown`, `su`). Root would bypass the directory-permission check and mask the lock test. Run plain `dotnet test` (not `--warnaserror`) to avoid a false SourceLink "unable to locate repository" error from the missing `.git`.

---

## Related

- [Coverage Tools](../reference/coverage-tools.md) — Tool usage reference (gap_check.py)
- [CRAP Per-Method Metric](../project/decisions/009-crap-per-method-metric.md) — Why per-method max CC, not class-level sum
- [Coding Standards](./coding-standards.md) — Code conventions
- [Orchestration Pitfalls](./orchestration-pitfalls.md) — How parallel agents collide through these global gates
