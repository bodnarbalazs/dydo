# Statechart provenance

Source: [cursor/plugins, pstack/skills/statechart](https://github.com/cursor/plugins/tree/1ba91fc25c0977741dd0e267849e6234b037ed0a/pstack/skills/statechart),
commit `1ba91fc25c0977741dd0e267849e6234b037ed0a`. This is the canonical source,
not the generated Claude harness copy. [LICENSE](LICENSE) is the pinned pstack MIT
license, Copyright (c) 2026 Lauren Tan.

The compiler, frontends, passes, backends, corpus and renderer guide are vendored.
Local changes:

- Explicit-only Codex metadata accompanies upstream Claude metadata. The description
  is human-facing. Formal-check use retains the model, config, bounds and sync commit.
- Folder-local Git attributes retain LF for executable shell scripts and the
  byte-compared corpus even when the host enables `core.autocrlf`.
- A native PowerShell launcher selects supported Node. Windows workers wait until
  a Job Object owns them; the job kills their descendants on exit. A detached Node
  janitor cleans task-owned scratch after normal or abrupt parent termination.
  POSIX launch and process groups retain upstream behavior.
- Streamed TLC/DOT lines normalize CRLF. Tool overrides resolve against the caller
  before TLC changes directory (upstream issue #973 / PR #1017). Empty output folders
  on Windows use non-recursive rmdir before rename; existing content is never removed.
- `exitClass` has an explicit literal return type. Two upstream async test callbacks
  discard their return values for the pinned local TypeScript check. Neither changes assertions.
- `tsconfig.json` adds `noUnusedLocals` and `noUnusedParameters`, which dydo's static gate
  checks with strict types; the unused bindings upstream left in byte-identical files stay.
  `worker.ts` and `native-entry.ts` write each part through `Buffer.from`, the same bytes as
  upstream's string-only conversion, without the arm no compiled part reaches.
- CLI output validation, worker launch and publication are local functions
  to meet the cognitive-complexity limit; verdict classification avoids a nested ternary.
  Native lifecycle tests retain real tool execution with absolute, relative, bare and
  unset `JAVA`, and controlled failure tests cover ownership and cleanup errors.
- Windows tool launches and recurring test subprocesses request hidden consoles,
  including Java discovery, real TLC and idle lifecycle fixtures.
  Job ownership, handshakes, termination and cleanup remain intact.
- Tests serialize files, normalize plain TLC line endings, and explicitly skip the
  upstream POSIX CLI/recording harness on Windows. All original cases remain for POSIX.
  Native Gherkin acceptance and Windows lifecycle tests are added, alongside the CRLF
  regression. Skips are not Windows proof.
- The renderer tests install nothing while they run. Upstream's `before` hook runs
  `npm install` for Pele 1.9.0, Mermaid 11.16.0 and jsdom 26.1.0. Here Pele is the
  managed module that `STATECHART_PELE` names. Mermaid and jsdom are dev dependencies,
  locked with fast-check in `scripts/pnpm-lock.yaml` and restored by
  `pnpm install --frozen-lockfile`, which runs no dependency build scripts. Without
  either, the renderer cases skip by name. Their cases and assertions are upstream's.
- dydo's native invocation enters through `native-entry.ts`. On Linux,
  `posix-lifetime.ts` checks inside the control channel and process group that dydo's
  native owner hands over; that owner performs scratch cleanup.
- Checker input must be the official TLC 1.7.4 jar by SHA-256. A `CommunityModules.jar`
  beside it is rejected as an unpinned dependency, and each run copies only the verified
  jar into owned scratch. Replay tests therefore need the real jar in `TLA2TOOLS` and
  skip without it. The config-fuzz oracle runs in Java's source-file mode and skips on
  a Java without a compiler, such as the managed JRE. CI's `statechart-config-oracle`
  job runs the unchanged seed-1, 4000-config comparison with the dependency lock's
  separate, hash-checked Temurin JDK 21.0.8+9 (the existing Java build-toolchain pin).
  Its TAP guard fails on skips or missing execution; the JDK is only provisioned on
  the hosted Linux runner, not as a managed runtime dependency.
- Original Balazs Bodnar MIT Java sources under `scripts/java/` route TLC through a
  loopback RMI socket bootstrap; SANY stays direct. TLC calls require the sealed helper
  and never fall back to raw TLC, whose listener would otherwise bind every interface;
  see [its readme](scripts/java/readme.md).

The Windows-era review and build records are at
[67cd2127](https://github.com/bodnarbalazs/dydo/tree/67cd21278872679398479601b99d95125678b7cd/dydo/project/pitfalls/dyd-415-statechart-evidence).

Dependencies and setup: [runtime](references/runtime.md). The original package test
script is `node --disable-warning=ExperimentalWarning --test test/*.test.ts`;
the local command adds `--test-concurrency=1` to protect a shared machine. Runtime
TLC still uses its upstream pinned one worker, fingerprint 0 and final liveness check.

## Upstream TypeScript blobs

Each upstream TypeScript file under `scripts/` at the pin, with its Git blob hash as
`git ls-tree` prints it. dydo's assurance gates measure a local TypeScript file whose
blob hash differs from its line here, or that has no line. A byte-identical file stays
vendored upstream code. Change this list only when the pin moves.

```upstream-blobs
dec8b2f6671f2d110cd880f7af8d2c714cdf23a3  scripts/backends/cfg.ts
327220cad69baa1d4fa5aa1cfe03f3cf9e13c133  scripts/backends/mermaid.ts
1ece33e8c45995493e84bc948ad613fce15481ab  scripts/backends/plain-text.ts
62e2a853375a1141491ef54fed8fd0d81a1bcd3c  scripts/backends/report.ts
4f6ade5a9a74c8e74a58125dc7a5d035ea2ca36b  scripts/frontends/config.ts
003dfc89d4925c6aed3e59073b6a9414da78117b  scripts/frontends/dot.ts
9b1eda59f12861b8fb981d6046741417cd1be667  scripts/frontends/marks.ts
e835b065e775a1b822eede5b1515d0401ce8356b  scripts/frontends/messages.ts
3fbe34f1a6b344c56cfd5afba3b90fe9a397c405  scripts/frontends/spec.ts
43387c12b314c343d7a046de2fd095a3950b0026  scripts/frontends/trace.ts
d73ddec9ee3fa6077c644625002707a96d0938a1  scripts/frontends/value.ts
4f2ddd925332773b68918c40e62fc79a3a32571b  scripts/frontends/verdict.ts
ed8a1b5f9f0b4104a901a966bdfd66f13102d5ce  scripts/frontends/view.ts
80fd403fe3c231b0986f239ba8c74a60b52c05f7  scripts/ir.ts
7d95d4fb57311c26bbbaca42eb283421f56140e0  scripts/passes/annotate.ts
cc8c197726137a0875462702e39cd07cbf55d547  scripts/passes/label.ts
15db7deda016faee9c0da33c2c419be51e528ed6  scripts/passes/lift.ts
7e5a3825adde46e8a625ad9e167cb0ce6b712174  scripts/passes/loop-check.ts
632a5c4ae6d6101c89f46803605c09b6a96f9b6f  scripts/passes/paths.ts
d5b6a521dc970f888d615d41caf692d0f7bcb2ec  scripts/passes/reach.ts
ae499230f5ba441d704386bd24df73cad3c92af7  scripts/passes/region-check.ts
bb7aea062410be42c25c779ca748c2b52c3e5a82  scripts/passes/regions.ts
f12a2e07f6e4fe283cfd1b2fc0d3f83dd3d75758  scripts/pipeline.ts
4efd368e5bcd0b4ef7cf8d8cfd1e511483c720aa  scripts/run.ts
4e2967e680895875d7d5352bfe997a4c08c2d1fc  scripts/statechart.ts
ef220432d79345fc5fa36210cef6c6ae7468fb4e  scripts/test/chart.test.ts
97b71c30c228eafd55f599375f16be4f656f20e5  scripts/test/cli.test.ts
88ec2d3d0b09cd85759507aaf786722add1a2cb7  scripts/test/config-fuzz.test.ts
8934373187b213d9be5ef6011db1893cbb8b037f  scripts/test/corpus.test.ts
c6aff9cadcc724202d7308a93fe2e17cc9aa64f4  scripts/test/differential.test.ts
b6634ad7ac998851cbce400bf5a52b37ee96ea80  scripts/test/java.ts
a3d149984e53558e8b6a59dd994cdbd28487aa2e  scripts/test/labels.ts
61af006968a6e9c2dc359c8b5859a1238b9e56cb  scripts/test/messages.test.ts
81a5056a37276e38bb4f68993378a82fb55036f5  scripts/test/pipeline.test.ts
434e25612db133034c5a9f7e04965906488ef01c  scripts/test/plain-tlc.test.ts
5309a92648b950554381f5adecacac161bee3e28  scripts/test/render.test.ts
7f27d04ee195e05d0ccea6c9e024a7b2890fde45  scripts/test/tlc-out.test.ts
f659423d24c489616168096695ecfde1714bf45d  scripts/test/tlc.ts
9559314668f6573e17db7c278d05f5776b235ebc  scripts/worker.ts
```

## Upstream delta check, 2026-10-29

The path's latest commit remains `519d36aa98955bf680db01bcde5fdfba686dc901`
(#981, wait for macOS process-group reaping), already in this pin. #982's qualified
action-name fix is also present. The SKILL.md rewrite remains `34b1b7c96f09f81a21a6c78f65b7833f7dcb5b24`.

Issues [#923](https://github.com/cursor/plugins/issues/923) (old harness temp paths /
LD_PRELOAD) and [#973](https://github.com/cursor/plugins/issues/973) (relative tool
paths) remain open. PRs [#1017](https://github.com/cursor/plugins/pull/1017),
[#997](https://github.com/cursor/plugins/pull/997),
[#924](https://github.com/cursor/plugins/pull/924),
[#890](https://github.com/cursor/plugins/pull/890),
[#844](https://github.com/cursor/plugins/pull/844),
[#819](https://github.com/cursor/plugins/pull/819) and
[#817](https://github.com/cursor/plugins/pull/817) are open and unmerged. The latter
four concern pre-rewrite interfaces; none is silently incorporated. No X announcement
was independently verified. Package versions and the license were read from the pin.
