# Exact command and proof trace

Working directory unless stated: `C:\Users\User\Desktop\Projects\DynaDocs\dydo\agents\workspace\dyd96-altcover-child-diagnosis`.

1. Copied the retained collision fixture and complete pinned tool directory; source and tool hashes are recorded in `research.md` — exit 0.
2. `dotnet restore fixture/SameLineChildProbe.csproj --configfile fixture/NuGet.Config` with fixture-local `APPDATA` — exit 0.
3. `dotnet build fixture/SameLineChildProbe.csproj -c Debug -f net10.0 --no-restore` — exit 0, 0 warnings/errors.
4. `.\.tools\altcover\altcover.exe --inputDirectory=fixture/bin/Debug/net10.0 --outputDirectory=fixture/instrumented-save --report=fixture/results/save.xml --reportFormat=OpenCover --save --verbose` — exit 0.
5. `dotnet SameLineChildProbe.dll` from `fixture/instrumented-save` — exit 0; `CHILD_ASSERT=1`, `PARENT_ASSERT=1`.
6. Preserved `fixture/results/save.xml.{0,1}.acv` byte-for-byte beneath `fixture/raw/save/` before collection.
7. `.\.tools\altcover\altcover.exe runner --recorderDirectory=fixture/instrumented-save --collect --outputFile=fixture/results/save-combined.xml --summary=N --verbose` — exit 0; spool 0: 11 visits; spool 1: 0 added; total 11.
8. Restored each retained spool separately and ran the same official `runner --collect` to `save-spool0.xml` and `save-spool1.xml` — exits 0; 11 parent visits and 13 child visits respectively.
9. Restored both retained spools and repeated collection to `save-combined-2.xml` — exit 0; again 11 total; SHA256 equals `save-combined.xml` (`B499378C42B7948794B92B9C7A6D08D7F835749AAB3F006BF5DF54F1BCD31C38`).
10. `.\.tools\altcover\altcover.exe --inputDirectory=fixture/bin/Debug/net10.0 --outputDirectory=fixture/instrumented-eager --report=fixture/results/eager.xml --reportFormat=OpenCover --eager --verbose` — exit 0.
11. `.\.tools\altcover\altcover.exe runner --recorderDirectory=fixture/instrumented-eager --workingDirectory=fixture/instrumented-eager --executable=dotnet --outputFile=fixture/results/eager-collected.xml --summary=N --verbose -- SameLineChildProbe.dll` — exit 0; both assertions; spool 0: 11, spool 1: 13, total 24.
12. Cloned official `https://github.com/SteveGilham/altcover.git` under `official-altcover/` and detached at exact commit `b698a5c2daa26ceb0723858914812538b6c10e81`. Read-only source inspection identified the compiled C# `AddTable` defect cited in `research.md`.

Raw proof: `fixture/raw/save/*.acv`; templates and collected XML: `fixture/results/*.xml`; exact pinned official source: `official-altcover/`.
