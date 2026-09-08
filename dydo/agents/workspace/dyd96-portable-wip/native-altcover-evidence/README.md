# DYD-96 portable AltCover evidence

This packet pins the native observations used by the bounded C# mechanism amendment. `physical-fixture/` proves physical MethodDef separation, zero-hit retention and method-local branch rows. `child-fixture/` preserves the same-line parent/child attack: `default-failure.opencover.xml` drops the later same-module child table, while `eager-success.opencover.xml` retains all 24 visits. The two source `.acv` tables discarded by the default fold are preserved byte-for-byte under `child-fixture/raw/`.

The four research/command records retain the exact commands, exits, tool identity, original failures and interpretation. Fixture files are source/project inputs only. The packet deliberately excludes the AltCover installation, official vendor clone, binaries, PDBs, `bin/`, `obj/`, dependency caches and instrumented output. The raw XML plus original `.acv` tables are sufficient to recheck the decisive report and aggregation facts; regenerated binaries are not identity substitutes for the hashes already recorded in the research.

`cfg-probe/` is a bounded specification-time calibration against the already pinned Roslyn and Sonar analyzer assemblies. Its source and result table demonstrate why raw CFG edge arithmetic is not the policy convention and pin the source decision values used by the amendment. Its generated `bin/` and `obj/` directories were removed after the successful run.

`SHA256SUMS` is authoritative for every other file in this directory and uses forward-slash paths relative to this directory.
