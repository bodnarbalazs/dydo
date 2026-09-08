# DYD-96 AltCover 9.0.102 native probe

**DECIDED — result:** AltCover 9.0.102 passes physical-method identity, zero-hit retention, branch ownership, CC and same-line/ordinal isolation when the instrumented program is the runner's direct target. It **fails the required child-process aggregation case**: the parent run found both parent and child `.acv` spools and both assertions passed, but the child-executed lambda remained wholly unvisited. A direct run of the identical instrumented child records that lambda correctly. This is a material native semantic gap, so AltCover is not yet a credible DYD-96 route and C# remains gap/exit 2 (`G = 2`). Full-suite integration was not attempted.

## Question and answer condition

Question: does pinned AltCover 9.0.102 natively preserve exact physical callable identity and method-local line/branch/CC facts for the retained async top-level fixture and a same-line child-process attack? A pass required exact pre-instrument MethodDef tokens once each, independent executed/unexecuted hits, actual descendant branch rows matching known IL arms, correct static CC, unchanged source/PDB mapping, and child-only hits merged into the final report. Destination: `dydo/agents/workspace/dyd96-altcover-probe/research.md` for the DYD-96 captain.

## What passed

The retained fixture source was copied byte-for-byte: SHA256 `2313F8E9A0AEDD19B61020177C7A13709D53286747E573A1264C55A628603D56`. The new build's pre-instrument identity is DLL `1321CD1940AFBC086DCCA5BEE3CC04A1BE3616FBA6976AA509174CBB32134F48`, PDB `6B02827DF803116CD97E45D58B364D9E3797F1A9030D41C509F1EB30596A0D54`, MVID `a2778924-4930-40d9-b0b9-37d90a25e498`. AltCover changed the instrumented DLL to `C76675CC595E87F37DC34BB3BF96DE450EB24540EE6F6D56F3C3A34C7C200C09` while preserving MVID and byte-identical PDB. The input DLL/PDB and source retained their pre-instrument hashes after collection; instrumentation used an output directory and did not rewrite source.

The collected OpenCover report (`fixture/results/altcover-collected.xml`, SHA256 `6263122F9B9307628ED5A14B029EE308A3A4CD52C6B051C4D0E10E2100C7A597`) contains these relevant physical rows exactly once:

| Token | Physical method | CC | sequence hit/rows | real branch hit/rows |
|---:|---|---:|---:|---:|
| 100663304 | async `MoveNext()` | 6 | 7/9 | 1/2 |
| 100663308 | executed `b__0_0(int)` | 2 | 6/9 | 1/2 |
| 100663309 | never-invoked `b__0_1(int)` | 1 | 0/3 | 0/0 |

The kickoff token `100663297`, generated wrappers/constructor (`100663298`, `100663299`, `100663305`), and `ProbeState.Record` (`100663302`) also remain separate rows; empty generated methods remain in the inventory. `ASSERT_EXECUTED=1` printed. No hit was borrowed by the never-invoked lambda. The lambda's two branch arms are descendants of token `100663308` at IL offset 7 and source line 5, with visits `1,0`; `MoveNext` separately owns its two assertion arms at offset 226, line 23, with visits `0,1`. This agrees with the fixture's known physical bodies and expected CC 2 for its one conditional lambda.

The report proves the synthetic-summary warning. There are exactly four descendant `<BranchPoint>` rows and two visited rows, but root `Summary` says 8/4. DYD-96 must count descendant rows (`vc > 0`), not summary branch totals or percentages. AltCover's pinned source documents why: OpenCover generation creates each `Method` from a Cecil `MethodDefinition`, writes its metadata token and method-local points ([official `OpenCover.fs`](https://github.com/SteveGilham/altcover/blob/b698a5c2daa26ceb0723858914812538b6c10e81/AltCover.Engine/OpenCover.fs#L237-L300)), while post-processing adds synthetic OpenCover-compatible branch summary counts ([official `PostProcess.fs`](https://github.com/SteveGilham/altcover/blob/b698a5c2daa26ceb0723858914812538b6c10e81/AltCover.Engine/PostProcess.fs#L373-L402)).

## Material child-process gap

The bounded attack fixture puts two conditional lambdas on source line 5. Both have method-local branch ordinals 0/1 and IL offset 2, which directly attacks `(line, ordinal)` aliasing. AltCover emits separate rows: executed token `100663301`, never-invoked token `100663302`, each CC 3 and each owning two branch rows. (CC 3 is AltCover's static IL result for the conditional expression body.)

In the parent-launched-child run, the child executed only the positive arm of token `100663301`; token `100663302` was never invoked. `CHILD_ASSERT=1` and `PARENT_ASSERT=1` printed, and the runner found two cleanly flushed spools: `altcover.xml.0.acv` (106 bytes) and `.1.acv` (111 bytes). Nevertheless, final `collision-fixture/results/altcover-collected.xml` (SHA256 `36912EC32416FECAD715E1F3E1F113436F5DC5209426A0F8169D011A7946B306`) reports both lambda methods unvisited, sequence `0/1`, branches `0/2`.

The isolating direct-child run used the same instrumented bytes and argument `child`. It printed `CHILD_ASSERT=1` and `collision-fixture/results/altcover-child-direct.xml` (SHA256 `3D8B16A27B3F9B1E8EC7F5B5CA7E29F399AB39C5E8C21C4C990F4258360ECAF4`) correctly reports token `100663301` visited, sequence `1/1`, branches `1/2`, while token `100663302` stays `0/1`, `0/2`. Thus same-line physical ownership works in direct collection; the observed loss is specifically across the child aggregation path. AltCover's source advertises per-process spooling and folding ([official `Tracer.fs`](https://github.com/SteveGilham/altcover/blob/b698a5c2daa26ceb0723858914812538b6c10e81/AltCover.Recorder/Tracer.fs#L44-L62), [official `Runner.fs`](https://github.com/SteveGilham/altcover/blob/b698a5c2daa26ceb0723858914812538b6c10e81/AltCover.Engine/Runner.fs#L994-L1029)); the native result does not satisfy that promise for this .NET 10 shape.

## Reproducibility and boundary

Pinned tool executable SHA256 is `21E522A496294D8E7B2DCDC598947E6B3B042307F8D164A40320A6877CA02025`; `altcover version` reported `9.0.102`. The collision pre/instrumented DLL hashes are `A6AF5E2524EAE980B99C597341837C7BDF819662BDB5D727CA76CDC822195678` / `1292AD64BD26024543CAF7E2EA7627FBFB7DF7F98EF4F80F6905FFF599EA53E1`; both preserve MVID `1c3410a0-dffc-4034-bdfe-38d1556c0fd5` and PDB SHA256 `807F82EA0121B0D653B54988DB7DDE72E2999428E08D9EDCACADF1B8FE55D5DA`. Exact commands and exit codes are in `commands.md`; raw template and collected reports are retained beneath each fixture's `results/`.

No production file, test suite, Git state, Linear state, collector, parser or policy was changed. No repository tests ran. All runner-launched processes exited; AltCover removed all `.acv` files after collection. The pinned tool and build outputs remain only under this Git-ignored owned directory. This probe establishes a concrete gap in AltCover 9.0.102 as invoked; it does not qualify other collectors or prove that the child behavior cannot be corrected by a future, separately reviewed integration mechanism.
