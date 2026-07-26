---
title: v223-3 Release
blocked-by: v223-2-upgrade-compatibility
due:
needs-human: false
priority: High
sprint: v2-2-3-upgrade-compatibility
status: in-progress
work-type: release
area: project
type: context
---

# v223-3 Release

Close the completed records, prove release readiness, and publish v2.2.3 through the repository's tag-triggered workflow.

## Task

1. Mark issues 0300-0305 resolved with concise evidence; include the already-written 0306 incident record and leave its 0307 follow-up open.
2. Run, in order:
   - `py DynaDocs.Tests/coverage/run_tests.py`
   - `py DynaDocs.Tests/coverage/gap_check.py --force-run`
   - `dotnet run --project DynaDocs.csproj -- check`
   - `dotnet build DynaDocs.csproj -c Release`
3. Set `DynaDocs.csproj` and `npm/package.json` to 2.2.3, then run:
   - `dotnet pack DynaDocs.csproj -c Release -p:Version=2.2.3 -o C:\tmp\dydo-v223-019f-package`
   - `dotnet tool install --tool-path C:\tmp\dydo-v223-019f-tool --add-source C:\tmp\dydo-v223-019f-package dydo --version 2.2.3`
   - `C:\tmp\dydo-v223-019f-tool\dydo.exe version`
4. Stage only the sprint root's release-owned allowlist, then verify `git diff --cached --check`, inspect `git diff --cached --name-only` against that allowlist, inspect the full cached diff for secrets/generated drift, and confirm unrelated `dydo/project/tasks/*.md` changes remain unstaged.
5. Preflight the irreversible boundary: confirm `master` is based on `origin/master`, `v2.2.3` is absent locally and remotely, `gh auth status` succeeds, and `.github/workflows/release.yml` still publishes from `v*`.
6. Commit the patch, push `master`, create annotated tag `v2.2.3` on that exact commit, re-verify both manifests from the tag, and push the tag.
7. Inspect the GitHub Actions release run with `gh run list --workflow release.yml --branch v2.2.3` and `gh run watch <run-id> --exit-status`.

## Success Criteria

- All gates are green and the staged tree contains only intentional release changes before commit; unrelated unstaged changes may remain.
- `git show v2.2.3:DynaDocs.csproj` and `git show v2.2.3:npm/package.json` both contain 2.2.3.
- The tag is present on the remote and its release workflow reaches a terminal success state.
- If a job fails without requiring source changes, diagnose and rerun it against the same immutable tag. If any source correction is required after publication begins, record the partial result and prepare v2.2.4; never move or replace v2.2.3.
