# DYD-96 `--showGenerated` marker evidence (2026-09-09)

Specification-time facts for the second bounded amendment: the `--showGenerated` marker contradiction (a) and the campaign-cap override (b). Worktree `C:/Users/User/Desktop/Projects/DynaDocs/.worktrees/dyd96-adoption`, branch `codex/DYD-96-assurance-adoption`, base HEAD `fb34c2bcbce1d0d344beb75dfdad3698b74d2c5e`, .NET SDK 10.0.300, AltCover 9.0.102 restored from `.config/dotnet-tools.json` (`dotnet tool restore --tool-manifest .config/dotnet-tools.json`, exit 0; `dotnet tool run altcover -- version` prints `AltCover version 9.0.102`). No repository campaign, `run_tests.py`, `gap_check.py` or .NET suite was run; one focused Python unittest case was run read-only; one `dotnet` or test process at a time throughout. Hashes are SHA-256, uppercase, of checkout bytes.

## Contradiction

The accepted prepare command (`DynaDocs.Tests/coverage/csharp_coverage.py` `altcover_commands`, pinned by `tests/test_csharp_coverage.py:20`) carries `--showGenerated`. `csharp_join.coverage_methods` rejects a negative sequence-point `vc` (`Invalid native sequence value for token ...`, line 68) and a negative branch-point `vc` (`Branch source ownership mismatch for token ...`, line 87) before any origin accounting. AltCover's own help (`dotnet tool run altcover -- --help`):

    --showGenerated        Optional: Mark generated code with a visit count
                             of -2 (Automatic) for the Visualizer if unvisited

## Retained evidence without the flag

Neither retained probe used `--showGenerated`. `physical-fixture/collected.opencover.xml` (commands 7 and 8 of `physical-probe-commands.md`: no `--eager`, `--localSource` or `--visibleBranches` either) holds the never-invoked lambda `System.Void Program/<>c::<<Main>$>b__0_1(System.Int32)` with three sequence points `vc="0"`, `visited="false"`, and the executed state machine `System.Void Program/<<Main>$>d__0::MoveNext()` with nine sequence points, two branch rows and `visited="true"`. `child-fixture/eager-success.opencover.xml` (`--eager` only) holds the never-invoked same-line lambda `System.Int32 Program/<>c::<<Main>$>b__0_1(System.Int32)` with one sequence point `vc="0"`, two branch rows `vc="0"`, `visited="false"`. `skippedDueTo` and a negative `vc` occur zero times in the three retained reports. The retained packet has no `GeneratedCodeAttribute`-attributed type or method, so it cannot say by itself whether dropping the flag changes anything for those; one scratch probe settles it.

## Commands

All from the worktree (tool manifest only); the fixture and every output live under the session scratchpad `C:/Users/User/AppData/Local/Temp/claude/C--Users-User-Desktop-Projects-DynaDocs/33b0f929-c23b-4923-bde1-b822a5ec586a/scratchpad/showgenerated-probe` (`<probe>` below), never in the repository tree. `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `MSBUILDDISABLENODEREUSE=1`.

1. `dotnet build Subject.csproj -c Debug --verbosity quiet -p:UseSharedCompilation=false` (cwd `<probe>`), exit 0, 0 warnings, 0 errors.
2. `dotnet tool run altcover -- --inputDirectory=<probe>/bin/Debug/net10.0 --outputDirectory=<probe>/with/instrumented --report=<probe>/with/report/template.opencover.xml --reportFormat=OpenCover --eager --localSource --visibleBranches --showGenerated --assemblyFilter=^(?!Subject$).*`, exit 0.
3. `dotnet tool run altcover -- runner --recorderDirectory=<probe>/with/instrumented --workingDirectory=<probe>/with/instrumented --executable=dotnet --outputFile=<probe>/with/report/coverage.opencover.xml --summary=N -- Subject.dll`, exit 0, `PROBE_ASSERT=1`, 42 visits.
4. As 2 without `--showGenerated`, into `<probe>/without/...`, exit 0.
5. As 3 for `<probe>/without/...`, exit 0, `PROBE_ASSERT=1`, 42 visits.
6. Throwaway comparison (scratch only, an `xml.etree` walk of both trees in parallel comparing the tag, every attribute and the text of every element): the reports of 2 and 4 (template) and of 3 and 5 (collected).
7. Campaign-cap facts (section below): an in-process call of `windows_job.validate` on four `request()` values under the pinned Python 3.12.14, then `python -m unittest tests.test_windows_job.WindowsJobTests.test_invalid_control_requests_never_launch_or_touch_outputs`, exit 0 (1 test OK).

The production command differs only in `--inplace` versus `--outputDirectory` (so one build could be instrumented twice) and in the three real input directories and assembly names; the marker is written by report generation, not by the placement of the instrumented assembly.

## Fixture

`Subject.csproj` (`05DC32EAFBBD6E348C378CD512BBD2843861623232DD96D22596E0A86ADCED9F`), `Directory.Build.props` (`AF0D58DB8C13B106AEE16B2132D515A350DCF8BCBA446B8B49C3EE11CE48D03F`) and `NuGet.Config` (`5256A7E3E07D2C5C94F7A1E6C45F39AAB011C659C5E2D53E452DEA525CE04575`) are byte-identical to the retained `physical-fixture/` files of the same role (`NativeLambdaProbe.csproj` renamed). `Program.cs` (`99D930D67E71719262EB45F0331E80E932B243A06812DA0DA68D0E3E2E6A1A1B`):

```csharp
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

if (GenType.Called(true) != 1)
{
    throw new InvalidOperationException("GenType.Called");
}

Plain.Lambdas();
if (await Plain.Awaited() != 6)
{
    throw new InvalidOperationException("Plain.Awaited");
}

GC.KeepAlive((Func<bool, int>)GenType.Never);
GC.KeepAlive((Func<bool, int>)Plain.NeverAttributed);
GC.KeepAlive((Func<int>)Plain.NeverCompilerGenerated);
GC.KeepAlive((Func<int>)Plain.NeverPlain);
GC.KeepAlive((Func<Task<int>>)Plain.NeverAwaited);
GC.KeepAlive((Func<IEnumerable<int>>)Plain.NeverIterated);
Console.WriteLine("PROBE_ASSERT=1");

[GeneratedCode("probe", "1.0")]
[CompilerGenerated]
public static class GenType
{
    public static int Called(bool flag)
    {
        return flag ? 1 : 2;
    }

    public static int Never(bool flag)
    {
        return flag ? 3 : 4;
    }
}

public static class Plain
{
    [GeneratedCode("probe", "1.0")]
    public static int NeverAttributed(bool flag)
    {
        return flag ? 5 : 6;
    }

    [CompilerGenerated]
    public static int NeverCompilerGenerated()
    {
        return 7;
    }

    public static int NeverPlain()
    {
        return 8;
    }

    public static void Lambdas()
    {
        Func<int, int> invoked = value => value > 0 ? 10 : 20;
        Func<int, int> neverInvoked = value => value > 0 ? 30 : 40;
        if (invoked(1) != 10)
        {
            throw new InvalidOperationException("invoked lambda");
        }

        GC.KeepAlive(neverInvoked);
    }

    public static async Task<int> Awaited()
    {
        await Task.Yield();
        return 6;
    }

    public static async Task<int> NeverAwaited()
    {
        await Task.Yield();
        return 9;
    }

    public static IEnumerable<int> NeverIterated()
    {
        yield return 11;
    }
}
```

The shapes it puts in one assembly: a `[GeneratedCode][CompilerGenerated]` type with an executed and a never-executed method, a `[GeneratedCode]` method, a `[CompilerGenerated]` method, a plain never-executed method, an invoked and a never-invoked `<>c` lambda with one branch each, an executed and a never-executed async state machine, a never-iterated iterator state machine, and the top-level async `<<Main>$>d__0::MoveNext` that executes and throws on no path.

## Reports

| Report | With `--showGenerated` | Without |
|---|---|---|
| `template.opencover.xml` | `67503C636A0B3D4CAC1E04DDD3CDB7BE35D9A88F2F8CA44FE290AA3843B280B9` (37,410 bytes) | `6041A5E491EF93ECF43F99A5650EACCBF9EC87AFF3DBD0A5CCE79A440D88A7D2` (37,351 bytes) |
| `coverage.opencover.xml` | `7C38793DD1B4D0582CE3C17946D7763A0E17A7D542FDB385B426BB291F08C751` (39,724 bytes) | `0B8F3C98BDBD3089C83F7880CF19DB14D299E58457AE758B52D366D8FD89E5E0` (39,700 bytes) |

Both pairs: module `Subject`, one `File` row (identical `fullPath`), 27 `Method` rows with identical `Name` and `MetadataToken`, 318 XML elements each, `skippedDueTo` absent from every element of all four reports.

## Decisive rows (collected reports)

`seq vc` and `br vc` list every `SequencePoint` and `BranchPoint` `vc` in document order; `uspid`, `ordinal`, `offset`, `sl`, `sc`, `el`, `ec`, `fileid`, `path`, `offsetend` and `bec` are identical in both reports for every row and are omitted.

| Method | token | visited with/without | seq vc with | seq vc without | br vc with | br vc without |
|---|---|---|---|---|---|---|
| `System.Threading.Tasks.Task Program::<Main>$(System.String[])` | 100663297 | true/true | [] | [] | [] | [] |
| `System.Void Program::.ctor()` | 100663298 | false/false | [] | [] | [] | [] |
| `System.Void Program::<Main>(System.String[])` | 100663299 | true/true | [] | [] | [] | [] |
| `System.Void Program/<<Main>$>d__0::MoveNext()` | 100663310 | true/true | [1, -2, -2, 1, 1, -2, -2, 1, 1, 1, 1, 1, 1, 1] | [1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1] | [-2, 1, 1, -2, 1, 1, 1, 1, 1, 1, 1] | [0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1] |
| `System.Void Program/<<Main>$>d__0::SetStateMachine(System.Runtime.CompilerServices.IAsyncStateMachine)` | 100663311 | false/false | [] | [] | [] | [] |
| `System.Int32 GenType::Called(System.Boolean)` | 100663300 | true/true | [1, 1, 1] | [1, 1, 1] | [1] | [1] |
| `System.Int32 GenType::Never(System.Boolean)` | 100663301 | true/false | [-2, -2, -2] | [0, 0, 0] | [-2] | [0] |
| `System.Int32 Plain::NeverAttributed(System.Boolean)` | 100663302 | true/false | [-2, -2, -2] | [0, 0, 0] | [-2] | [0] |
| `System.Int32 Plain::NeverCompilerGenerated()` | 100663303 | true/false | [-2, -2, -2] | [0, 0, 0] | [] | [] |
| `System.Int32 Plain::NeverPlain()` | 100663304 | false/false | [0, 0, 0] | [0, 0, 0] | [] | [] |
| `System.Void Plain::Lambdas()` | 100663305 | true/true | [1, 1, 1, 1, 0, 0, 1, 1] | [1, 1, 1, 1, 0, 0, 1, 1] | [1, 1, 0, 1] | [1, 1, 0, 1] |
| `System.Threading.Tasks.Task`1<System.Int32> Plain::Awaited()` | 100663306 | true/true | [] | [] | [] | [] |
| `System.Threading.Tasks.Task`1<System.Int32> Plain::NeverAwaited()` | 100663307 | false/false | [] | [] | [] | [] |
| `System.Collections.Generic.IEnumerable`1<System.Int32> Plain::NeverIterated()` | 100663308 | false/false | [] | [] | [] | [] |
| `System.Int32 Plain/<>c::<Lambdas>b__3_0(System.Int32)` | 100663314 | true/true | [1] | [1] | [1] | [1] |
| `System.Int32 Plain/<>c::<Lambdas>b__3_1(System.Int32)` | 100663315 | true/false | [-2] | [0] | [-2] | [0] |
| `System.Void Plain/<Awaited>d__4::MoveNext()` | 100663317 | true/true | [1, 1, 1, 1] | [1, 1, 1, 1] | [1, -2] | [1, 0] |
| `System.Void Plain/<Awaited>d__4::SetStateMachine(System.Runtime.CompilerServices.IAsyncStateMachine)` | 100663318 | false/false | [] | [] | [] | [] |
| `System.Void Plain/<NeverAwaited>d__5::MoveNext()` | 100663320 | true/false | [-2, -2, -2, -2] | [0, 0, 0, 0] | [-2, -2] | [0, 0] |
| `System.Void Plain/<NeverAwaited>d__5::SetStateMachine(System.Runtime.CompilerServices.IAsyncStateMachine)` | 100663321 | false/false | [] | [] | [] | [] |
| `System.Void Plain/<NeverIterated>d__6::System.IDisposable.Dispose()` | 100663323 | false/false | [] | [] | [] | [] |
| `System.Boolean Plain/<NeverIterated>d__6::MoveNext()` | 100663324 | true/false | [-2, -2, -2] | [0, 0, 0] | [] | [] |
| `System.Int32 Plain/<NeverIterated>d__6::System.Collections.Generic.IEnumerator<System.Int32>.get_Current()` | 100663325 | false/false | [] | [] | [] | [] |
| `System.Void Plain/<NeverIterated>d__6::System.Collections.IEnumerator.Reset()` | 100663326 | false/false | [] | [] | [] | [] |
| `System.Object Plain/<NeverIterated>d__6::System.Collections.IEnumerator.get_Current()` | 100663327 | false/false | [] | [] | [] | [] |
| `System.Collections.Generic.IEnumerator`1<System.Int32> Plain/<NeverIterated>d__6::System.Collections.Generic.IEnumerable<System.Int32>.GetEnumerator()` | 100663328 | false/false | [] | [] | [] | [] |
| `System.Collections.IEnumerator Plain/<NeverIterated>d__6::System.Collections.IEnumerable.GetEnumerator()` | 100663329 | false/false | [] | [] | [] | [] |

Template reports (before any run): every `Method` is `visited="false"` in both; with the flag, 39 sequence points and 20 branch points already carry `vc="-2"` (every point of `<<Main>$>d__0::MoveNext`, `GenType::Called`, `GenType::Never`, `Plain::NeverAttributed`, `Plain::NeverCompilerGenerated`, both `<>c` lambdas, `<Awaited>d__4::MoveNext`, `<NeverAwaited>d__5::MoveNext` and `<NeverIterated>d__6::MoveNext`), without it all are `vc="0"`; `Plain::NeverPlain` and `Plain::Lambdas` are `0` either way.

## Complete difference inventory

Element-by-element comparison, every attribute of every element (command 6):

| Report | Attributes that differ (occurrences) | Anything else |
|---|---|---|
| template | `SequencePoint.vc` 39, `BranchPoint.vc` 20 | none |
| collected | `SequencePoint.vc` 21, `BranchPoint.vc` 8, `MethodPoint.vc` 6, `SequencePoint.bev` 7, `Method.visited` 6, `Method.sequenceCoverage` 7, `Method.branchCoverage` 6, `Method.crapScore` 7, `Summary.visitedSequencePoints` 15, `Summary.visitedBranchPoints` 14, `Summary.sequenceCoverage` 15, `Summary.branchCoverage` 14, `Summary.visitedMethods` 13, `Summary.visitedClasses` 4, `Summary.minCrapScore` 13, `Summary.maxCrapScore` 15 | none |

Negative `vc` with the flag: template `SequencePoint` 39, `BranchPoint` 20; collected `SequencePoint` 21, `BranchPoint` 8, `MethodPoint` 6. Without the flag: none. `Module`, `Files`/`File`, `Class`, `Method` `Name`/`MetadataToken`/`cyclomaticComplexity`/`nPathComplexity`/`isConstructor`/`isStatic`, and every point identity attribute are identical; `bev`, `visited`, the per-method coverage attributes and the `Summary` rows are derived from `vc` and follow it.

## Reading

1. The marker is applied at prepare time to every point of every attribute- or compiler-generated body, whether or not the body will execute (`GenType::Called` and the top-level `MoveNext` carry `-2` in the template and real counts after the run).
2. After collection an unvisited point keeps `-2` wherever it sits, including inside executed bodies: the untaken branch of `<Awaited>d__4::MoveNext` (`[1, -2]`) and the unreached throw paths of `<<Main>$>d__0::MoveNext`. The marker is therefore not a per-method "never executed" fact that a consumer could verify from the report.
3. Under the flag a never-executed generated method is `visited="true"` with `sequenceCoverage="100"`, `branchCoverage="100"` and its `Summary` counts every point as visited; the vendor summaries contradict the vendor counts.
4. Without the flag the same methods, files, sequence points and branch rows are present with `vc="0"` and `visited="false"`; the `<>c` lambdas and every async and iterator `MoveNext` body remain instrumented and reported with their branch rows; nothing is skipped.

Route: remove `--showGenerated` from the pinned prepare command. Decoding `-2` in the join was rejected because the report carries no field that states the documented condition (generated and unvisited), the marker lands inside executed bodies, and the assembly producer records `CompilerGeneratedAttribute` but not `GeneratedCodeAttribute`, so a decoder would have to accept a negative number as zero exactly where DR 048 requires an honest native zero hit. The existing negative checks in `csharp_join.py` stay as the fail-closed guard.

## Campaign-cap facts

`windows_job.py` at HEAD: `validate(value)` hard-codes `(("execution_seconds", 1800), ("teardown_seconds", 10))` (line 96); `run(value, environment=None)` calls `validate(value)`; `request(argv, cwd, output, execution_seconds=60, teardown_seconds=10)`; `main()` reads the request from stdin and calls `run(value)`. In-process under the pinned Python 3.12.14 (command 7), `validate(request([node, "-e", "process.exitCode=0"], root, root/"evidence", seconds, 10))`: 1800 passes; 1801, 14400 and 14401 raise `ValueError: Invalid owner deadline`; nothing is launched and no output directory is created. The existing `test_invalid_control_requests_never_launch_or_touch_outputs` passes at HEAD (1801 among its rejections). Callers of the owner: `run_tests.py:220` (`run(request(..., execution_seconds=1800, teardown_seconds=10), isolated_environment())`, in-process), `tests/test_javascript_metrics.py:22` (`execution_seconds` 300 through a copied controller and its stdin `main()`), and `tests/test_windows_job.py` (budgets of at most 3 seconds). All sit under the default; only the DYD-103 mutation caller needs an explicit maximum, which the specification routes as an in-process keyword of `run` and `validate`, never as a request field.

## Result

Dropping `--showGenerated` changes nothing but the `-2` marker and the fields AltCover derives from it; the accepted non-negative `vc` rule and the clause that generated bodies backed by maintained source are measured both hold with the flag absent. The campaign cap stays the one constant 1800 at the owner boundary, raised only per call by an explicit caller maximum.
