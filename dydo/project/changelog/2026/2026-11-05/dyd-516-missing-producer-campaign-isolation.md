---
area: project
type: changelog
date: 2026-11-05
---

# DYD-516 missing-producer campaign test runs in a child interpreter

## Summary

The Python runner suite no longer kills unrelated children of its own unittest process
([DYD-516](https://linear.app/bodnar-balazs/issue/DYD-516)). The contract
`AssuranceCampaignTests.test_assurance_campaign_publishes_evidence_when_the_producer_is_absent`
called `run_tests._run_assurance_campaign` in the unittest process. On Linux that reaches
`_run_linux_campaign`, which makes its caller a subreaper for good through
`PR_SET_CHILD_SUBREAPER`. Its cleanup then kills and reaps every live descendant of the caller,
including children that the test process started for other reasons. The DYD-504 round-two review
found this residue of its N3 finding; the round-one fix had changed only the reviewed failure
fixture.

The test now runs the campaign through `campaign_in_child`, a module-level helper in
`test_runner.py` that starts a fresh interpreter, as `LinuxCampaignTests.campaign` already did.
That helper now serves both, so the real Linux containment still runs for this contract, in a
process the test owns. The contract is unchanged: exit 2, no artifacts, `cleanup_confirmed`, and
`subject_status` 2. The test also asserts two things across the call. The unittest process's
`PR_GET_CHILD_SUBREAPER` flag stays as it was, and a sentinel child started before the call is
still alive afterwards. On Windows the flag reads as `None`, the sentinel check still applies, and
the child goes through the native job owner as before. On Linux a failed `prctl` read raises
`OSError` with its errno, so a failed read can't pass for an unchanged 0.

The child inherits the Python coverage producer's `sitecustomize` through `PYTHONPATH` and exits
through `SystemExit`, so its lines of `_run_assurance_campaign` and `_run_linux_campaign` are still
measured. No production code changed; `LinuxCampaignTests`, `test_reaping.py` and the other
subreaper tests keep their real native paths.

## Proof

Proof commit `8a463704` adds the two assertions around the unchanged in-process call. Run alone in
a fresh unittest interpreter on base `8a386aca`, it fails with `(0, None) != (1, 0)`: the flag goes
from 0 to 1, and the sentinel is gone. A pass-through probe on `os.waitid` shows the campaign's
`reap_adopted` collecting the sentinel as `CLD_KILLED` with status 9, an exit of -9. Because the
campaign took that status, `Popen.poll()` can only report 0. On the fix the test passes, and the
same probe in child mode reports flag 0 before and after and the sentinel still running. Restoring
the in-process call fails the test again with the same tuple.

## Files Changed

`DynaDocs.Tests/coverage/tests/test_runner.py` (proof commit `8a463704`, the DYD-516 implement
commit, then the DYD-516 fix commit for the `prctl` read), the N3 wording in [DYD-504's toolchain entry](../2026-11-04/dyd-504-toolchain.md), and this
entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
