---
area: project
type: reference
---

# Native primitive replay source archive

The DYD-415 captain's [finite eleven-path pin](https://linear.app/bodnar-balazs/document/dyd-415-finite-archival-pin-eleven-native-primitive-replay-inputs-44a93d431114)
authorizes the source representation described here. Captain verified all 27
members, eleven checkout/Git pairs and 22 reference/dependency hashes, then
authorized removal of only the eleven originals in disposition
`8d539a4a-7d59-4795-bf67-311b6cd8dc25`. The maintained driver records that removal
under `../implementation2/archival-removal/`; every other historical path remains
protected. Historical execution and aggregate static/coverage retry remain held.

`native-primitives-replay-86931b9c.zip` is a standard ZIP. Its exact map is
`native-primitives-replay-86931b9c.manifest.json`. `checkout/` entries preserve the
eleven original checkout files, and `git-blobs/` entries preserve their exact Git
blobs separately. `stage-context/` entries preserve the five distinct older
script/navigation variants explicitly mapped by the pinned proposal's two docs
stage receipts. These are copies; no additional old path is relocated.

The manifest binds the captain pin and proposal, original paths/commits/modes/blob
IDs/raw hashes, exact entry set, existing stage input archives/receipts, source
dependencies, reference map and restoration instructions. Raw logs, failed results
and existing input ZIPs remain at their recorded paths. It makes no claim that the
final86931 fixture is the fixture used by every earlier gate.

Restore only into fresh expressly owned scratch, never over current source. Use
the explicit map, reject unsafe paths, duplicate/case-colliding names and links,
create files exclusively, and compare every byte/hash/length before considering
source fidelity established. The commissioned implementation2 fidelity receipt is
under `../implementation2/archival-fidelity/`. That check does not import or execute
the restored files, compile/restore a project or run a historical test.

The manifest is an immutable pre-removal checkpoint snapshot. Its
`originalFilesStillPresent` and `stage` fields record that earlier checkpoint;
its inherited `restoration.fidelity: "NOT EXECUTED"` is proposal text. The actual
executed fidelity receipt, `../implementation2/archival-fidelity/fidelity.json`,
records the later successful non-executing check. These historical fields are
preserved verbatim, rather than rewritten to describe the current representation.

A separately commissioned runtime replay needs Git3527ef64 plus the selected
stage's exact input overlay and recorded command context, including its explicit
RepositoryRoot and initially absent test lockfile. Old wrappers contain hardcoded
worktree/TEMP paths and source checks; they cannot execute unchanged from arbitrary
scratch. Preserve them verbatim and map recorded child commands through separately
approved orchestration. Compiler/OS/NuGet/apt binary availability and identical
future binaries are not proved by a source archive.

Current product work uses maintained `../implementation2/launch.py` and
`../implementation2/driver.py`, with its own exact source manifests and receipts.
The reviewed fix1 producers, current implementation2 drivers and nine maintained
corpus views remain measured. This archive represents only the eleven pinned
paths, not the remaining71 historical association findings. Aggregate static stays
exit2/incomplete until a separately authorized valid measurement establishes
otherwise. DYD-434 remains Done.

## Related

- [Original native milestone and historical commands](../verification.md)
- [Statechart verification history](../../../dyd-415-statechart-verification.md)
