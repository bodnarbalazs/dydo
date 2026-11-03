# B3 A - stopped at first unexpected checker result

code-writer/model: gpt-6.1-sol/high. HEAD
6e3fe7e8018130af6b8586ef0cf40c1474740864 retained; no commit.

The one released group-A invocation exited 1. Seven of eight planned Java
commands executed. The seventh, `action-bound-reference`, returned 12 rather
than the pinned 0. Its original stdout reports `Invariant BelowTwo is violated`
with the ordered trace x=0,1,2 and two Next transitions. It reports 3 generated,
2 distinct states; it does not report successful completion. The worker stopped
with `ValueError: Unexpected TLC code for action-bound`. The action-bound
bootstrap command did not execute. No retry or source/expectation correction
occurred. This is a failed/incomplete group, not an eight-command parity PASS.

The preceding good, invariant-bad and liveness-bad reference/bootstrap pairs
produced their pinned semantic results. Their saved facts and raw stdout remain
under `retained/output/`. The three bootstrap processes have passive raw
PID/starttime/FD/tcp/tcp6 observations and process-owned 127.0.0.1 listener
observations. This partial result cannot supply the missing action-bound pair.
It exercised manually isolated official checker DATA, not production TS/native
checker isolation, installed invocation or platform acceptance.

[Original failure](stop.json), [worker diagnostic](dyd415-rmi-b3-2bde5afcdbfb45ab-finite-start.stderr),
[exact argv](retained/output/action-bound-reference.argv.json),
[raw result](retained/output/action-bound-reference.result.json) and
[raw stdout](retained/output/action-bound-reference.stdout) retain the failure.
[Teardown](teardown.json) plus three raw absence receipts confirm removal of
both owned containers and their volume. [FIFO release](released.json) and
[Heavy/TEMP release](secondary-released.json) confirm resource release.
Owned TEMP scratch remains data at
`C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-3dyl3z6t`.

[D admission](../b3-20261031a-D/D-admission-gate.json) remains 24 actual passed
contract cases, zero skips, with cleanup/custody confirmed before A. D's contract
tests did not establish the real action-constraint outcome that failed here.

[Post-custody](post-custody.json) records unchanged source/historical pins, HEAD,
product and payload. The additional return inventory binds the retained D/A
evidence and rechecks the admitted inputs without executing any driver/test.
No B/C, Node acquisition, compiler, native/installed test or healthy B1/B2 rerun
was performed. The independent Node source packet is not prepared at this
failure return boundary. No network request/download occurred.

Desktop Java HOLD, unavailable Windows access and Java assurance, remaining
product/platform/static/coverage/review gates and both settled-channel
no-contact rules remain in force. Return this precise failure to captain for
disposition before any correction or renewed execution; no readiness claim.
