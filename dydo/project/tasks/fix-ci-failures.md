---
area: general
name: fix-ci-failures
status: human-reviewed
created: 2026-05-06T20:21:51.8691208Z
assigned: Brian
updated: 2026-05-06T20:44:39.8658037Z
---

# Task: fix-ci-failures

Review commit 834b00f on master — two test-only fixes that turned Linux CI from red to green (run 25459952823, build+test 1m44s).

CHANGES (DynaDocs.Tests/ only, zero production code touched):

1. DynaDocs.Tests/Integration/InquisitionTests.cs (InitGitRepo helper)
   - Was: RunGit("init") then RunGit("commit --allow-empty -m \"init\"").
   - Now: second call passes -c user.email=test@example.com -c user.name=Test inline so the commit succeeds on CI runners with no global git config.
   - Picked option 2 from Adele's brief (single git invocation, no global state mutation, no helper signature change). WorktreeMergeSafetyIntegrationTests uses option 1 ("git config user.email" calls) — kept divergence intentional because option 2 is meaningfully cleaner here (no extra RunGit calls).
   - Fixes 5 failures: InitGitRepo_CompletesAndProducesValidRepository + Coverage_ReportWithDate_ShowsDateAndStatus + Coverage_WithReports_ShowsTableHeader + Coverage_MixedReports_ShowsCorrectStatuses + Coverage_ReportWithMultipleDates_ShowsLatest.

2. DynaDocs.Tests/Services/ProcessUtilsCaptureTests.cs (RunProcessCapture_EnvironmentInjected_PreservesParentEnv, line 81-83)
   - Was: sh -c "echo $DYDO_TEST_VAR-${PATH:0:1}".
   - Now: sh -c "echo $DYDO_TEST_VAR-${PATH%%:*}".
   - Root cause: ${PATH:0:1} is a bashism. /bin/sh on Debian/Ubuntu is dash, which rejects it as "Bad substitution" (exit 2 — exactly what CI saw). ${PATH%%:*} is POSIX (strips the first colon-separated component), preserves semantic intent (proves parent PATH is inherited via a non-empty prefix). Inline comment updated to match.
   - Fixes 1 failure: RunProcessCapture_EnvironmentInjected_PreservesParentEnv.

VERIFICATION:
- run_tests.py: 4131/4131 passed locally on Windows.
- gap_check.py: 140/140 modules pass tier requirements.
- gh run watch 25459952823: CI green on Linux (1m44s, all stages incl. Test).

KEY DECISIONS:
- Used -c <key>=<value> form rather than `git config` to keep test isolation tight and avoid mutating any state outside the single commit invocation.
- Did not add a regression guard in CI that strips global git config — out of scope; the inline -c form is self-contained.
- The test suite worked locally before the fix (Windows dev box has global git config + cmd path uses %PATH:~0,1%); only Linux CI was red. Fix is verified by the green CI run, not by local repro.

NO PRODUCTION CHANGES. NO UNRELATED CHANGES. NO OUT-OF-SCOPE WORK.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 834b00f on master — two test-only fixes that turned Linux CI from red to green (run 25459952823, build+test 1m44s).

CHANGES (DynaDocs.Tests/ only, zero production code touched):

1. DynaDocs.Tests/Integration/InquisitionTests.cs (InitGitRepo helper)
   - Was: RunGit("init") then RunGit("commit --allow-empty -m \"init\"").
   - Now: second call passes -c user.email=test@example.com -c user.name=Test inline so the commit succeeds on CI runners with no global git config.
   - Picked option 2 from Adele's brief (single git invocation, no global state mutation, no helper signature change). WorktreeMergeSafetyIntegrationTests uses option 1 ("git config user.email" calls) — kept divergence intentional because option 2 is meaningfully cleaner here (no extra RunGit calls).
   - Fixes 5 failures: InitGitRepo_CompletesAndProducesValidRepository + Coverage_ReportWithDate_ShowsDateAndStatus + Coverage_WithReports_ShowsTableHeader + Coverage_MixedReports_ShowsCorrectStatuses + Coverage_ReportWithMultipleDates_ShowsLatest.

2. DynaDocs.Tests/Services/ProcessUtilsCaptureTests.cs (RunProcessCapture_EnvironmentInjected_PreservesParentEnv, line 81-83)
   - Was: sh -c "echo $DYDO_TEST_VAR-${PATH:0:1}".
   - Now: sh -c "echo $DYDO_TEST_VAR-${PATH%%:*}".
   - Root cause: ${PATH:0:1} is a bashism. /bin/sh on Debian/Ubuntu is dash, which rejects it as "Bad substitution" (exit 2 — exactly what CI saw). ${PATH%%:*} is POSIX (strips the first colon-separated component), preserves semantic intent (proves parent PATH is inherited via a non-empty prefix). Inline comment updated to match.
   - Fixes 1 failure: RunProcessCapture_EnvironmentInjected_PreservesParentEnv.

VERIFICATION:
- run_tests.py: 4131/4131 passed locally on Windows.
- gap_check.py: 140/140 modules pass tier requirements.
- gh run watch 25459952823: CI green on Linux (1m44s, all stages incl. Test).

KEY DECISIONS:
- Used -c <key>=<value> form rather than `git config` to keep test isolation tight and avoid mutating any state outside the single commit invocation.
- Did not add a regression guard in CI that strips global git config — out of scope; the inline -c form is self-contained.
- The test suite worked locally before the fix (Windows dev box has global git config + cmd path uses %PATH:~0,1%); only Linux CI was red. Fix is verified by the green CI run, not by local repro.

NO PRODUCTION CHANGES. NO UNRELATED CHANGES. NO OUT-OF-SCOPE WORK.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-06 20:59
- Result: PASSED
- Notes: Surgical, test-only fix. InitGitRepo: -c key=val on the commit invocation is the right scope (no global mutation, helper signature unchanged); divergence from WorktreeMergeSafetyIntegrationTests' git-config form is acceptable. ProcessUtilsCaptureTests: /c/Users/User/bin is correct POSIX (longest :* suffix removed -> first PATH entry remains as a non-empty prefix), preserves the test's intent; comment explains the dash-vs-bash WHY. run_tests.py 4131/4131 pass; gap_check.py 140/140 modules pass (exit 0). dydo check has 56 errors but all pre-existing on parent 49c0759 and tracked by existing issues (#0159 inquisition type, #0166 hub/index drift, etc.) - none introduced by this commit.

Awaiting human approval.