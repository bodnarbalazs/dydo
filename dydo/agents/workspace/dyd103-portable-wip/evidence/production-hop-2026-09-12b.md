# DYD-103 production hop evidence — 2026-09-12b (admiral-authorized blocker fixes)

Captain: issue-captain/model: deepseek/deepseek-v4.1-flash (effective identity unavailable).
Branch `DYD-103-mutation-gate`, base `83c89856e6cccc52cdff876ec520eeac3b74d0db`,
worktree `.worktrees/dyd103-prod`, resumed at `4bef01ff` on the admiral's 2026-09-12 ruling.

## Blocker 2 — resolved (consumer drift, gate 8 aggregate 2)

The admiral authorized amending the spec and the owned adapter to the merged, reviewed DYD-96
producer. Two drifts existed, both found by running the real producer through the adapter validator
in this worktree:

1. `excluded[].origin` for `native-evidence-fixture`: the spec pinned `{manifest, manifestSha256}`,
   the producer (`gate_inventory.py:71-73`) emits `{manifest, entry, sha256, manifestSha256}`.
2. `projects[].compile`: the spec pinned *sorted* paths, the producer
   (`gate_inventory.assemble_inventory`, `gate_inventory.py:157-158`) emits the evaluated-project
   order unsorted. The adapter reads membership from `sources[].projects`, never from
   `projects[].compile`, so compile order carries no selection meaning.

Corrections: `mutation_adapter.INVENTORY_ORIGIN_KEYS["native-evidence-fixture"]` now carries the
four producer keys; the `projects` validator accepts canonical-and-unique compile order-insensitively;
`specification.md`'s origin-field and projects field tables and the new `Ruled — 2026-09-12` entry
record both. DYD-96 was not edited.

Proof: the real inventory produced by `gate_adapter._inventory_artifact` over this worktree now
passes `mutation_adapter._validate_inventory` — 3431 files, 285 sources, 4 excluded, 6 projects,
exit 0 (previously `invalid inventory: excluded row …`). Gate 1 stayed 163 OK; gate 10 stayed 3 OK.

## Blocker 1 — partially resolved; one residual DYD-113 site the ruling forbids

The admiral authorized editing only `test_testing_facade.py::test_active_manifest`. That edit is
made (both classes now expect the configured mutation row) and cleared the `DynaDocs uses its real
adapters and admits missing assurance` scenario.

A second DYD-113 expectation fails for the same root cause, and it does not live in the authorized
file. The Reqnroll scenario `Schema 1 has one small concrete manifest and result shape` runs the C#
step `AssertConcreteManifestShape` (`TestingFacadeSteps.cs:210-222`), which asserts
`JsonElement.DeepEquals(active gap_check.json stacks[0], documented stacks[0])`. The documented
inline dotnet stack in `DynaDocs.Tests/Features/testing-facade.feature` (around line 210) still
carries `"mutation": {"state": "unavailable", "reason": "Pending DYD-103"}`; a full flattening diff
shows the mutation row is the *only* difference from the active stack. Making the scenario green
requires the one-line inline mutation row to become the configured adapter row — the same update
DYD-96 made for its own static/coverage rows in its docs hop `c9ffeb16` ("DYD-96 docs: configure
dotnet assurance rows"). The admiral's ruling names `Features/testing-facade.feature` as
do-not-touch, so this edit was not made.

Gate 3 full isolated suite (with `$env:PYTHON="$P"`) is therefore Failed 1, Passed 2483, Skipped 2,
Total 2486 — the single failure being that Schema scenario. Gate 9 was not run: its coverage rows
run the same suite and cannot pass while this scenario fails.

## Gates on the current tree

| # | Result |
|---|---|
| 1 `test_mutation*.py` | 163 OK |
| 4 Reqnroll `MutationAssurance` (`$env:PYTHON="$P"`) | 65/65 passed |
| 5 `dotnet build … --warnaserror` | 0 warnings, 0 errors |
| 6 `dydo check` | 0 errors, 0 warnings |
| 7 `capabilities` | exit 0, mutation configured on all three stacks |
| 10 replay probe | 3 tests OK (44.3 s), real engines |
| 3 full suite | Failed 1 / Passed 2483 — Schema inline mutation row (above) |
| 9 `--force-run` | not run — blocked by gate 3 |
| 8 `gate mutation --since 2e31b1d0…` | not run — the candidate must still change for the residual feature edit, and the spec binds gate 8 to the exact candidate SHA |

## Resume state

- Gate-10 hop from the previous session at `d84400a8` stands; this session adds the blocker-2 fix
  (adapter + spec + `owned-paths.json`), the authorized probe edit, and this evidence.
- Remaining authorization needed: the one-line inline mutation row in
  `DynaDocs.Tests/Features/testing-facade.feature` (mirroring `c9ffeb16`). Then gate 3 → green,
  gate 9, and detached gate 8 on the frozen candidate, then review/PR/DYD-131.
- No campaign is running; no mutation lock or snapshot remains.
