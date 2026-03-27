---
area: reference
type: reference
---

# Coverage Tools

An in-house Python script for measuring and enforcing test coverage. Located in `DynaDocs.Tests/coverage/`.

---

## `gap_check.py` — Tier Compliance

Self-contained tier compliance checker. Runs tests via `dotnet test`, collects Cobertura XML coverage from Coverlet, and checks every source module against its tier's requirements. Exits with code 0 (all pass) or 1 (failures).

```bash
python DynaDocs.Tests/coverage/gap_check.py                    # auto-detect: skip or run tests
python DynaDocs.Tests/coverage/gap_check.py --force-run        # always run tests
python DynaDocs.Tests/coverage/gap_check.py --detail           # show uncovered lines in failures
python DynaDocs.Tests/coverage/gap_check.py --inspect Guard    # inspect modules matching 'Guard'
```

**Auto-skip:** gap_check automatically skips tests when no source or test files have changed since the last coverage run. When skipping, it reuses existing coverage data. Use `--force-run` to override this and always run tests. A plain `dotnet test` does not produce coverage data — only gap_check's own test invocation (with Coverlet flags) does.

### What it checks (per module, against assigned tier)

| Metric | T1 | T2 | T3 |
|--------|----|----|-----|
| Has test file | required | required | required |
| Line coverage | >= 80% | 100% | 100% |
| Branch coverage | >= 60% | >= 80% | 100% |
| CRAP score | <= 30 | <= 15 | <= 5 |

### Tier detection

All modules default to T1. Higher tiers are declared with a comment annotation in the first 10 lines of the **test file**:

```csharp
// @test-tier: 2
```

### Tier registry

Promotions are tracked in `DynaDocs.Tests/coverage/tier_registry.json` (committed to git). Adding a `@test-tier` annotation auto-registers the module. Removing an annotation without manually editing the registry produces an error — this prevents accidental demotions.

### Exclusions

The following are excluded from compliance checks:
- Generated code (`/obj/`, `*.g.cs`, `*.generated.cs`)
- The `Program` class (entry point)
- Pure data models with ≤ 3 executable lines and no test coverage

### CRAP calculation

`CRAP = CC² × (1 - line_coverage)³ + CC`

Cyclomatic complexity is the **per-method maximum** extracted from Cobertura XML `<method>` elements, not the class-level sum. This avoids penalizing classes that have many simple methods.

---

## Directory Structure

```
DynaDocs.Tests/coverage/
├── gap_check.py              # Tier compliance checker
├── tier_registry.json        # Tracks T2/T3 promotions (committed to git)
└── coverage.runsettings      # .NET Coverlet configuration
```

---

## Related

- [Testing Strategy](../guides/testing-strategy.md) — Tier definitions and thresholds
- [CRAP Per-Method Metric](../project/decisions/009-crap-per-method-metric.md) — Why per-method max CC
