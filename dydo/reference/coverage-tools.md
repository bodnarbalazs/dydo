---
area: reference
type: reference
---

# Coverage and Assurance Tools

`DynaDocs.Tests/coverage/gap_check.py` is the project testing facade. Its adjacent
`gap_check.json` is the schema 1 manifest that declares each stack's test, static, coverage, and
mutation adapters.

```powershell
py DynaDocs.Tests/coverage/gap_check.py --help
py DynaDocs.Tests/coverage/gap_check.py all
py DynaDocs.Tests/coverage/gap_check.py test --stack dotnet -- --filter FullyQualifiedName~ParserTests
py DynaDocs.Tests/coverage/gap_check.py gate static --stack dotnet
py DynaDocs.Tests/coverage/gap_check.py gate mutation --since BASE --stack dotnet
py DynaDocs.Tests/coverage/gap_check.py --force-run
```

`all` runs tests only. The three `gate` operations remain explicit. `--force-run` is the
compatibility full-G operation: it selects test, static, and coverage for every stack, and exits 2
while any selected row is unavailable or invalid. Mutation is separate.

The facade runs argv arrays without a shell. It appends native test arguments only after `test ... --`.
Configured gate rows declare required artifacts, which must exist before a successful child becomes a
passing gate. Each run writes a machine-readable `result.json` below the configured artifact root
and prints its location. Results preserve raw child exits and aggregate to 0 for pass, 1 for a
measured failure, 2 for invalid or unavailable work, and 130 after interrupted adapter cleanup.

DynaDocs currently has a verified worktree-isolated .NET test adapter, Python and Node conformance
adapters, and unavailable static/coverage (DYD-96) and mutation (DYD-103) rows. It does not claim a
complete G or M gate yet.

DR 048's adopted static policy is: warnings as errors and strict types; no dead code; passing tests
and a test file for every non-trivial module; 80% line and 60% branch coverage per module; HCRAP and
cognitive complexity at most 20 per method; at most seven nonconstructor parameters; no supported
nested ternary; no clone meeting both 15 lines and 100 tokens; and no dependency cycles. Only code
not maintained here (generated, vendored, or minified) is excluded. It has no tiers, classic CRAP
threshold, registry, annotation, suppression, or nesting-depth gate. Mutation is separate: DynaDocs
requires no surviving or uncovered changed-code mutants.

The canonical portable runner and manifest are
`dydo/reference/gap-check.example.py` and
`dydo/reference/gap-check.example.json`. The source files are byte-identical; the example
is intentionally unfinished until its ASP.NET, React/Vite, and Python/uv rows have real project paths
and verified evidence.

## Related

- [Testing Strategy](../guides/testing-strategy.md)
- [DR 048](../project/decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md)
