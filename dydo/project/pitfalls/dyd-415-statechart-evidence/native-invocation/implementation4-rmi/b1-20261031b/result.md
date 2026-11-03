---
area: project
type: reference
---

# B1b: live admission and helper tests passed; first compiler failed

`code-writer/model: gpt-6.1-sol/high`. Same retained uncommitted draft at
`6e3fe7e8018130af6b8586ef0cf40c1474740864`. Authority is the captain admission
correction SHA256 `1b140b26cf5db532e55d36861ea4dc6821282a3e1f7bbbea66302a76dab3ca69`.
This is the one corrected attempt; the original [B1 failure](../b1-20261031a/result.md)
and its exact driver/launcher remain unchanged.

## Actual admission and containment

The approved normal host call observed real `C:/Users/User/AppData/Local` and
normal TEMP. Both new file sources check Windows and the effective namespace
before driver startup/booking. No environment, ticket or Namespace was injected.
The [preflight](preflight.json) binds the live prefix `LC-Suite-Slot`, variable
`LC_SUITE_SLOT_TICKET`, exact LC hashes and unchanged build sources.

The driver [owned the live FIFO](admission.json), ticket
`0000000001-c47e8f210eba6417`, with `inherited=false`. Its nonce was
`3c45eefdd1d54237`. It then acquired nonwaiting Heavy and the exclusive normal
TEMP lock. Hidden child argv receipts retain the inherited ticket. Input snapshot
hashes are in [source-inputs.json](source-inputs.json). The full successor source
diffs are [driver](driver-source.diff) and [launcher](launcher-source.diff);
the executable sources remain at their named normal TEMP `.py` paths.

All three owned containers used the exact cached Python image
`sha256:6a9220edda062e9223ea0784740a535987299420b96fbfd520b89bbd3e772485`.
Inspection before start records network-none, no ports, cap-drop ALL,
no-new-privileges, read-only root/input, bounded128pids/1GiB/2CPUs and disjoint
owned work/output. JDK extraction alone wrote the new owned labelled volume;
the compiler mounted it read-only. No desktop Java or global install occurred.

## Actual result and first failure

[Twelve helper tests](dyd415-rmi-bootstrap-3c45eefdd1d54237-tests-start.stderr)
were discovered and executed: **12 passed, zero skipped**, no Java invoked by
those tests. Their raw input hash is
`423d8d3eed6a58a869e37f5ae4838657b202dd1ac845ad95443323dbafcb30cb`.

The official JDK download was207098019 bytes and matched pinned SHA256
`f2dc5418092c43003db8f9005c4a286e1c0104fea96ccdd49e8ebd037cac9219`.
The unchanged build recipe verified the extracted archive/toolchain and entered
the first `javac --release 11 -Xlint:all -Werror -proc:none` invocation. It exited1:

```text
warning: [path] bad path element "/input/CommunityModules.jar": no such file or directory
error: warnings found and -Werror specified
1 error
1 warning
```

The exact [compiler argv](failed-build/first/javac.command.json),
[exit](failed-build/first/javac.result.json), [stderr](failed-build/first/javac.stderr)
and empty stdout were copied without byte edits from the retained owned work.
Offline `tar -xOf` inspection of the exact pinned TLC jar's
`META-INF/MANIFEST.MF` confirms `Class-Path: CommunityModules.jar`.
That manifest-relative compile input is absent in the pinned two-input recipe.
No replacement jar was fetched, no warning/flag was relaxed and no input jar or
source was modified. This identifies the concrete recipe/dependency obstacle;
it is not successful compilation or a runtime failure.

The first class directory and output directory were empty when read after exit.
The second compilation, deterministic jar packaging and comparison never ran.
There is **no helper artifact or build receipt**. The canonical lock stays
unbuilt. No automatic source fix or second attempt followed this first failure.
Raw container streams, stop receipt and original TEMP scratch remain retained.

## Owned cleanup and limits

[Teardown](teardown.json) confirms removal of exactly three owned containers:

- `732fbc2b19386d7845261605efebe296c4ce34af3fdee18878cdf1a24c6bee78`
- `2457ff1a2b2067c2d9b028e7e23f46c9ab450888c21bb82c4e3c0fac0c65a722`
- `3b360dc6a089cab182d891e17bce457eb8361a8f38cea0d91f3d8dbc3f5d8788`

Their exact IDs/owner labels were checked before removal and absence was read
afterward. The owned volume `dyd415-rmi-bootstrap-3c45eefdd1d54237-jdk` was
likewise checked, removed and confirmed absent. [Heavy and TEMP lock release](secondary-released.json)
and [FIFO release](released.json) are recorded. LC owned its ticket lifecycle;
no queue files, foreign locks or processes were manually removed.

Product/build/fixture/LC source, payload declarations and dependency lock were
not changed by this successor. No sockets, bootstrap, TLC/model, chart, readiness,
installer, npm/native/platform campaign or desktop Java ran. Existing source
corrections remain unexecuted. Helper-test success is not Java metrics, static,
coverage, runtime, Windows, review or readiness PASS. All those holds, PS
retirement and both settled human-conversation no-contact rules remain.
