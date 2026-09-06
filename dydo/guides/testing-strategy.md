---
area: guides
type: guide
---

# Testing Strategy

Every project exposes one project-local testing facade. It is a small Python runner beside a schema
1 JSON manifest. The facade selects declared adapters and executes their argv arrays directly: it
does not construct a shell command, infer an omitted gate, or turn missing assurance into success.

Run the project runner:

```powershell
py DynaDocs.Tests/coverage/gap_check.py all
py DynaDocs.Tests/coverage/gap_check.py --force-run
py DynaDocs.Tests/coverage/gap_check.py gate mutation --since BASE
```

## Stable grammar

`test --stack NAME -- ARGS` runs only one selected test adapter and forwards the arguments
after `--` as literal argv items. `all` runs every declared test adapter by default, or the
selected stacks in manifest order. `gate static`, `gate coverage`, and `gate mutation --since
BASE` run only that capability. `capabilities` checks the same stack, isolation, command and path
contracts without running children or creating result artifacts. Valid configuration, including
declared unavailable capabilities, returns 0; malformed entries are reported as invalid alongside
valid peers and return 2. Mutation's argv structure is checked without requiring `--since` for inspection.
`--force-run` selects every test, static, and coverage row; it never runs mutation.

Bare invocation prints help, creates no result, and exits 2. A recognized operation writes one
`result.json` under the manifest's repository-contained `artifactRoot`. It records schema,
candidate commit and dirty state, operation, selected stacks, ordered rows, and aggregate exit.
Each row records its stack, capability, state, argv, working directory, isolation requirement and
evidence, raw child exit, result exit, artifacts, and any reason.

Exit 0 means every selected configured row passed. Exit 1 means a measurement failed. Exit 2 means
invalid, missing, malformed, unsupported, or unavailable work. Exit 130 means an interrupted
adapter completed cleanup. The aggregate preserves that priority: interruption, then unavailable or
invalid work, then measured failure, then pass.

## Manifest and adaptation

The manifest has schema `1`, a repository-relative artifact root, and an ordered array of uniquely
named stacks. A stack declares `name`, `kind`, `cwd`, `isolation`, and all four capabilities:
`test`, `static`, `coverage`, and `mutation`. A configured capability owns an `argv` or
`current-python` command and artifact declarations; `current-python` prefixes argv with the
running interpreter. An unavailable capability has a reason and is a failed-closed result, not a
passing gate.

Manifest cwd, adapter and artifact paths are repository-relative and contained. A configured non-test
gate must declare required artifacts and create or observably refresh each one during its successful
child invocation. The facade compares file metadata and content, recursively for directory artifacts;
an unchanged old report cannot pass. It never deletes or modifies old evidence to manufacture freshness.
Mutation has exactly one argv item equal to `{base}`; the
facade replaces that one item with `--since`'s value. Isolation is a project adapter claim:
in-place work has direct evidence, while worktree and per-run requirements name a verified adapter.
The facade does not invent isolation.

DR 048 has one policy for every maintained module: warnings as errors and strict types, no dead code,
all tests passing and a test file for every non-trivial module,
line coverage of at least 80%, branch coverage of at least 60%, HCRAP at most 20 per method,
cognitive complexity at most 20, at most seven parameters outside constructors, no supported nested
ternary, no clone meeting both 15 lines and 100 tokens, and no namespace or module dependency cycles.
Only code not maintained here (generated, vendored, or minified) is excluded. There are no tiers, classic CRAP thresholds,
registry, annotations, per-file suppressions, or nesting-depth gate. Mutation is separate: DynaDocs requires
no surviving or uncovered changed-code mutants. A stack
without a reviewed mechanism reports that gate as unavailable until adoption.

Use `dydo/reference/gap-check.example.py` with its adjacent
`dydo/reference/gap-check.example.json` as a starting point. Rename both together to
`gap_check.py` and `gap_check.json` at the chosen project location. The runner discovers the enclosing
Git root; paths in the manifest resolve from that root. Outside Git, they resolve from the runner's
folder. The two distributed runner sources are byte-identical.

Replace each project's cwd and artifact placeholders, supply the real isolation adapter, then enable
its capability with faithful argv. The ASP.NET example shows `dotnet test` but leaves execution
unavailable until a worktree adapter copies working changes. The React/Vite example shows Node
running `node_modules/vitest/vitest.mjs run`; its adapter must arrange per-run artifacts. The Python
example uses `uv run --locked --extra dev -m pytest` from the adapted Python project directory.
A fully adapted targeted test can run while other stacks remain unfinished. Default `all` still
reports every declared test row and returns 2 until all selected tests are available and valid.

Global JSON/schema/request errors start nothing. Row-local defects skip only that row; valid peers
run before aggregate failure is reported. Configured rows require command and artifacts and forbid a
reason. Unavailable rows require a reason, forbid executable commands/artifacts, and may carry
non-executable `exampleArgv` for adoption. Do not relabel an unwired available mechanism as a pass.
The final operational static/coverage and mutation adoption remains DYD-96/103/91 work.

An interrupt goes to the active adapter process group. The facade grants up to 30 seconds for adapter
cleanup before escalation, preserves raw child exit when observed, and records exit 130. Adapters
own cleanup; the router does not invent worktree or artifact isolation. DynaDocs' real cancellation
probe uses a safe filtered test and verifies its newly observed worktree directory and Git
registration have disappeared before the result is reported.

## Related

- [Coverage Tools](../reference/coverage-tools.md)
- [DR 048](../project/decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md)
