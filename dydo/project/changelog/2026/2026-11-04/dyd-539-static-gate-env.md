---
area: project
type: changelog
date: 2026-11-04
---

# DYD-539 static-gate environment moves to `.static-gates/python`

## What changed

The local assurance gates' pinned Python environment moved from
`dydo/_system/.local/static-gates/python` to the repo-root, gitignored `.static-gates/python`
(`bin/python` on POSIX, `Scripts/python.exe` on Windows). The guard refuses every agent write into
`dydo/_system/**`, so before this change each fresh worktree waited for Balazs to run a user-only
setup script (DYD-538, DYD-533). An agent now provisions the environment itself, with
`python3.14 -m venv .static-gates/python` and a `pip install` of
`DynaDocs.Tests/coverage/requirements.lock`, or links a matching prepared environment. The
`dydo/_system/**` protection, the guard, the off-limits policy, every lock and every threshold are
unchanged.

- `DynaDocs.Tests/coverage/gate_run.py`: `STATIC_GATE_INTERPRETER` names the new path, so
  `gate_adapter`, `gate_collect` and the coverage tests follow it.
- `.github/workflows/release.yml` and `ReleaseWorkflowTests` create, install and invoke the new path.
- `.gitignore` ignores `/.static-gates/`. Gate inventories read `git ls-files` with
  `--exclude-standard`, and MSBuild's default excludes skip dot folders, so the environment stays
  outside every scan.
- `dydo/reference/coverage-tools.md` and the testing strategy's facade section give the new path and
  the Linux and Windows commands.
- `DynaDocs.Tests/Integration/GuardStaticGateProvisioningTests.cs` proves the guard allows the
  documented route and still refuses `ln`, `touch` and `cp` into `dydo/_system/**`.

## Guard behaviour the route relies on

Venv creation, the `ln -s` link and the gate invocations pass outright. Every `pip install` meets the
guard's install nudge: it refuses the first try once, and the identical retry passes when the user or
the ticket being executed approved that install. The new test pins this warn behaviour. Against the
old location, the documented link is a hard off-limits block.

## Proof

Red: with `StaticGateEnvironment` set back to the old path, the documented `ln -s` case fails with
`BLOCKED: Command references off-limits path`. Against the old `release.yml`,
`ReleaseWorkflow_ValidatesTheBuildBeforeEveryPublicationAction` fails. Against the old
`gate_run.py`, `test_tools_name_both_interpreters_and_every_resolved_tool_pin` fails. Green: the
focused `dotnet test` filter `GuardStaticGateProvisioningTests|ReleaseWorkflowTests` passes 34 of 34.
On a provisioned tree, the gate inventory holds 0 paths under `.static-gates/` and MSBuild holds 0
items there for `DynaDocs.csproj`, `DynaDocs.Tests.csproj` and `GateMetrics.csproj`. Raw logs and the
base-versus-candidate gate comparison are in the DYD-539 implement return.
