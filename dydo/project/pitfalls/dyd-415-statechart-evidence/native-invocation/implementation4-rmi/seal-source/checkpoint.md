# B1c distribution binding — source/data checkpoint

code-writer/model: gpt-6.1-sol/high. HEAD remains
`6e3fe7e8018130af6b8586ef0cf40c1474740864` on `DYD-415-statechart`.
The retained dirty draft is authorized. No commit, reset or integration occurs.
Authority is the [captain binding pin](https://linear.app/bodnar-balazs/document/dyd-415-inspected-b1c-artifact-binding-and-finite-b2-source-checkpoint-52e50ff5ac1a).

The canonical helper is a byte-for-byte copy of the separately inspected
[B1c artifact](../b1-20261031c/statechart-tlc-bootstrap.jar): 1,825 bytes,
SHA256 `1772e71e2d82f470f115fa7e6d63205c388f84a0fa55537f120b9d53f3d3fbfa`.
There are exactly two major55 class members, with no manifest or nested jar.
The immutable [build receipt](../b1-20261031c/build-receipt.json) remains SHA256
`60498308cfb5290f093a453e0ab20f4378ed9d35e1624304aa8d75ddad12c8ac`.
It binds the original build lock SHA256
`ab5d4052b97eb06e43d050ded02bac16b47f87421c44e154228d974c3e86a028`.

The canonical lock now says `sealed-build` and `built-not-runtime-validated`,
records this independent artifact/receipt identity and verified build-only JDK
facts, and retains the original Java sources, compiler/member/toolchain
expectations and recipe. It is a sealing delta after compilation. B1c's raw
receipt, pre-sealing lock, failed B1/B1b outcomes and source pins are unchanged.
No artifact was rebuilt, nor was an expected hash derived from unpinned drift.

[Before identities](before.json) retain raw pre-edit copies of each sealing
document, lock, payload declaration and association file. The
[payload row declaration](payload-row-diff.json) was written before either
payload declaration was regenerated. It states every old row, inspected source
identity and proposed new row: five existing rows change and nine are added.
The source closure is the inspected
[runtime-isolation pin](../runtime-isolation/source-pin.json); documentation and
build-lock changes are the explicit sealing delta. All other 37 payload rows,
metadata, versions and `dependency-lock.json` remain unchanged. The
[after identities](after.json) bind resulting declaration/source bytes and the
limited source/data checks actually performed.

The three new maintained B2 Python files have genuine contract tests in
`DynaDocs.Tests/coverage/tests/test_statechart_rmi_b2.py`, associated in the
existing manifest. They are not archived, disguised, excluded or marked as
assurance passes. Those cases and native discovery remain unexecuted at this
checkpoint. [B2](../b2-source/checkpoint.md) is a finite source proposal awaiting
captain inspection and explicit execution release.

Only hash, ZIP member, JSON, XML and source/reference reading occurred here.
No tests/discovery, compiler, Java, Docker/container, TLC, readiness or installer
was executed. This binding is candidate distribution data, with no payload
integration/runtime verdict. Java assurance remains unavailable through the
adopted collector, Windows access is unestablished, desktop Java remains held,
and supported PowerShell paths remain. Model/chart semantics, installed
readiness, lifecycle, platform, static/coverage, review and current-master gates
remain open. Neither settled human thread was contacted.
