# Exact command trace

Working directory unless noted: `C:\Users\User\Desktop\Projects\DynaDocs\dydo\agents\workspace\dyd96-altcover-probe`.

1. `dotnet tool install altcover.global --version 9.0.102 --tool-path dydo/agents/workspace/dyd96-altcover-probe/.tools/altcover` — exit 0 (invoked from repository root; official NuGet download).
2. `.\.tools\altcover\altcover.exe version` — exit 0; `AltCover version 9.0.102`.
3. `dotnet build fixture/NativeLambdaProbe.csproj -c Debug -f net10.0` — exit 1; sandbox denied the user-level `AppData\Roaming\NuGet\NuGet.Config`. No compiled output.
4. `dotnet restore fixture/NativeLambdaProbe.csproj --configfile fixture/NuGet.Config` — exit 1 for the same user-config read; no compiled output.
5. `$env:APPDATA=(Resolve-Path 'fixture/.appdata').Path` then `dotnet restore fixture/NativeLambdaProbe.csproj --configfile fixture/NuGet.Config` — exit 0.
6. `$env:APPDATA=(Resolve-Path 'fixture/.appdata').Path` then `dotnet build fixture/NativeLambdaProbe.csproj -c Debug -f net10.0 --no-restore` — exit 0, 0 warnings/errors.
7. `.\.tools\altcover\altcover.exe --inputDirectory=fixture/bin/Debug/net10.0 --outputDirectory=fixture/instrumented --report=fixture/results/altcover.xml --reportFormat=OpenCover --verbose` — exit 0.
8. `.\.tools\altcover\altcover.exe runner --recorderDirectory=fixture/instrumented --workingDirectory=fixture/instrumented --executable=dotnet --outputFile=fixture/results/altcover-collected.xml --summary=N -- NativeLambdaProbe.dll` — exit 0; `ASSERT_EXECUTED=1`; one 127-byte spool; 20 visits.
9. `$env:APPDATA=(Resolve-Path 'fixture/.appdata').Path` then `dotnet restore collision-fixture/SameLineChildProbe.csproj --configfile collision-fixture/NuGet.Config` — exit 0.
10. `$env:APPDATA=(Resolve-Path 'fixture/.appdata').Path` then `dotnet build collision-fixture/SameLineChildProbe.csproj -c Debug -f net10.0 --no-restore` — exit 0, 0 warnings/errors.
11. `.\.tools\altcover\altcover.exe --inputDirectory=collision-fixture/bin/Debug/net10.0 --outputDirectory=collision-fixture/instrumented --report=collision-fixture/results/altcover.xml --reportFormat=OpenCover --verbose` — exit 0.
12. `.\.tools\altcover\altcover.exe runner --recorderDirectory=collision-fixture/instrumented --workingDirectory=collision-fixture/instrumented --executable=dotnet --outputFile=collision-fixture/results/altcover-collected.xml --summary=N -- SameLineChildProbe.dll` — exit 0; `CHILD_ASSERT=1`, `PARENT_ASSERT=1`; two spools (106/111 bytes); final lambda coverage missing.
13. `.\.tools\altcover\altcover.exe runner --recorderDirectory=collision-fixture/instrumented --workingDirectory=collision-fixture/instrumented --executable=dotnet --outputFile=collision-fixture/results/altcover-child-direct.xml --summary=N -- SameLineChildProbe.dll child` — exit 0; `CHILD_ASSERT=1`; one 111-byte spool; executed lambda correctly visited with 1/2 branches.
14. `Get-ChildItem -Recurse -Filter *.acv dydo/agents/workspace/dyd96-altcover-probe` — exit 0 from repository root; no files returned after collection.

Setup copied the retained fixture's `Program.cs`, project, `Directory.Build.props`, and `NuGet.Config` with `Copy-Item`; hashes were checked before build. The collision fixture was authored only beneath the owned ignored directory. Inspection used PowerShell's XML DOM and `System.Reflection.Metadata` read-only APIs; no collector or parser was implemented.
