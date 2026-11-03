# Nine Node acquisition contracts: source-only execution envelope

code-writer/model: gpt-6.1-sol/high. Retain HEAD
6e3fe7e8018130af6b8586ef0cf40c1474740864 and the existing draft. No commit.
Captain's single-bootstrap/Node-envelope preparation release governs this packet.

This packet proposes one exact native unittest discovery invocation selecting
only test_statechart_node_acquisition.py. [Input manifest](input-manifest.json)
binds the existing nine names and four unchanged files: that test, acquisition
driver, launcher and transfer. They match the inspected Node source pin
bb193c726b2b665d3b05cad3aa6768b9dd4c12227fb60584cd53df4d78f493fa.
No test/source/assertion change, archive/runtime input, Node extraction, setup,
network, compiler, Java or Node execution is part of this envelope.

[Commands](commands.json) pins the actual proposed launcher, driver, worker and
native child argv. The worker verifies exact four-file membership and hashes
before discovery and again after the child returns. Discovery must execute all
nine distinct expected methods, with raw exit0, nine named ok lines, the native
Ran9/OK summary, no missing/unselected/duplicate execution, skip/failure status
or unexpected stdout. Source inventory is NOT actual discovery or a PASS.

The 180-second test bound is enforced inside the Linux worker with
Popen.wait(timeout=180). The child starts a new owned session. Timeout/error
cleanup kills only its owned process group if still running and waits at most
5 seconds; raw PID/stat, argv, exit, drain and elapsed time are retained before
interpretation. preexec_fn sets the hard kernel RLIMIT_FSIZE to 16MiB per
regular file before unittest exec, bounding each raw stdout/stderr file. Thus
the bound does not depend solely on a Docker attach timeout or output polling.
The unchanged shared helper's outer 1200-second Docker transport timeout remains
separate. Genuine source-bound tests start no subprocesses and perform no real
network requests. Their local mock response/file exercises are not acquisition.

The host driver reuses the exact inspected b3-source driver container/run/save
mechanism (24f0afe8f437cef9dc0fc8a3d5cf98540f39af0bf6f45e129f12c2e53549c100),
hash-checked before import. No shared helper edit or new runner framework.
Four sources are copied exclusively into fresh registered input/repo, with
before/after hashes. New worker/manifest copies are separately bound. Input is
read-only; only owned work/output are writable. One labelled owned container,
no volume/toolchain, cached pinned Linux/amd64 image, pull never/network none,
read-only root/input, cap-drop ALL/no-new-privileges,128PIDs/1GiB/2CPUs; no
public ports, host namespaces, Docker socket, privilege or global setup.
Actual ID/image/owner/settings/mounts are inspected before start.

The normal-TEMP launcher and driver reject redirected LOCALAPPDATA/TEMP/live
namespace, verify HEAD/source/custody/LC, then book the default live LC FIFO,
take nonwaiting Heavy and the exclusive owned TEMP dydo-fullsuite.lock. They
inherit legitimate tickets without env spoof or private namespace. Host children
are hidden. Cleanup verifies exact owned ID/name/label before stop/removal,
records raw absence and leaves scratch/output as DATA. Uncertain ownership or
drain retains the TEMP lock. First unexpected failure stops without correction,
retry or foreign cleanup. The proposed output is b3-node-contract-20261031a/;
it has not been created by this preparation.

No execution-release.json is supplied. The source pin is an inspection packet,
not permission to import/discover/run the tests or containers. Only static AST
syntax/name reading and file hashing/copying prepared it. New wrapper modules
remain maintained source; their own unit/coverage/association assurance has not
been established here. No fake association, exclusion, inventory/policy change,
new measurement tool or assurance waiver is introduced. Final gates remain owed.

Historical contracts-only authority is preserved at
../b3-action-20261031a-contracts/execution-release-at-run.json with original
SHA2562585e8f01e9ee02f20988a0da46821dcd83a46fb965cd0f83da9daf4e8e6c908.
Captain superseded only current administrative action authority for bootstrap;
old contracts and Node-return receipt bytes/pointers are not rewritten. Their
original logical release path maps to that exact at-run copy. Completed prior
evidence is preserved; the independent in-flight bootstrap output is not frozen
as an execution input and will be bound separately in the combined return.

Desktop Java/javac/install/probe HOLD, Windows access/prompt observability and
Java assurance gaps, B/C and all final installed/native/npm/platform/static/
coverage/current-master/independent review/readiness/publication holds remain.
Both settled HITL channels remain NO CONTACT. Return only to captain/records.
