# DYD-431 findings register: dydo 3.6.0 release inquisition

Audit SHA `d8ca1786b456e09eb138819328b45cb25dc68a59` (feature/dydo-3-6-0, the DYD-859 merge, PR #322).
Landing base `42882830292f44133f796ee3d152cb446ea4091f` (merge-base with master `7df0143d`).
Time-boxed, about 2.5 hours from 15:45Z (Balazs, P-DYD-46, 2026-11-07 15:45Z).

Severity: blocker = must not ship (P-DYD-46 Bug); should and nit = P-DYD-51 (3.7.0) Bug and release-notes line.

| ID | Area | Severity | Where | Wrong | Proof | Sightings | Tags | Disposition |
|---|---|---|---|---|---|---|---|---|
| F-01 | A-01 | should (crew: blocker; captain: pre-existing on 42882830, 3.6.0 no worse, guard-misread lane moved to 3.7.0 by the 15:45Z freeze; admiral may promote) | Services/BashCommandAnalyzer.cs:951 | `git -C . push -f`, `git --no-pager push --force`, `git -C . push origin --delete` allowed | red 7b6d58d9 GitGlobalOptionHidesForcePush_IsRefused (proof/DYD-431-A-01-1) | — | — | DYD-865 |
| F-02 | A-01 | should (as F-01) | Services/BashCommandAnalyzer.cs:951 | `git push -fu` / `-uf` not read as force push | red 89fe62d4 CombinedShortFlagHidesForcePush_IsRefused | — | — | DYD-865 |
| F-03 | A-01 | should (as F-01) | Services/BashCommandAnalyzer.cs:2014, :2142 | protected delete inside if/for/( )/{ }/lone &/pwsh block allowed | red ceac5ccf CompoundSyntaxHidesProtectedDelete_IsRefused | — | — | DYD-866 |
| F-04 | A-01 | should (as F-01) | Services/BashCommandAnalyzer.cs:2142 | env/command/timeout/nohup/xargs or /bin/rm hides protected delete | red 56b0da37 WrapperHidesProtectedDelete_IsRefused | — | — | DYD-867 |
| F-05 | A-01 | should (as F-01; in-flight fix) | Services/BashCommandAnalyzer.cs:1811 | `2>`, `1>`, `>|` to protected file allowed | red c7982a7c FdRedirectWritesProtected_IsRefused | — | re-check on final tip after DYD-640 | sighting on DYD-640 |
| F-06 | A-01 | should (as F-01) | Services/BashCommandAnalyzer.cs:2203 | `sed -i` over several files checks only the last | red b68bdfc4 SedInPlaceFirstOfManyFilesIsProtected_IsRefused | — | — | DYD-868 |
| F-07 | A-01 | should | Services/BashCommandAnalyzer.cs:2153 | `git rm` / `git checkout --` change protected file; contradicts dydo/files-off-limits.md:122-124 | red c4efd2e1 GitSubcommandChangesProtected_IsRefused | — | — | sighting on DYD-337 (pre-existing backlog) |
| F-08 | A-01 | should | Services/BashCommandAnalyzer.cs:45 | `find -delete` removes protected file | red 817fe883 FindDeleteRemovesProtected_IsRefused | — | — | DYD-869 |
| F-09 | A-01 | should | Services/BashCommandAnalyzer.cs:2153 | pwsh `[IO.File]::WriteAllText` to protected file | red dede6517 DotNetFileApiWritesProtected_IsRefused | — | — | DYD-870 |
| F-10 | A-01 | should | Services/BashCommandAnalyzer.cs:61, :2294 | `dd of=<protected>` read as path `of=…` | red 81084550 DdOutputFileWritesProtected_IsRefused | — | — | DYD-871 |
| F-11 | A-01 | nit | OffLimitsService.IsPathProtected | `echo x > dydo//index.md` allowed | none (nit) | — | — | DYD-872 |
