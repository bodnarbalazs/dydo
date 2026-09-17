# Pinned source-CC calibration

The fixture was compiled against the already built, locked Roslyn 5.9.0 and SonarAnalyzer.CSharp 10.33.0.1635 assemblies from `DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0`. No repository suite ran.

Commands (ordinary pipes, exit 0):

```powershell
$env:APPDATA=(Resolve-Path 'dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence/cfg-probe').Path
dotnet restore dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence/cfg-probe/CfgProbe.csproj --configfile dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence/physical-fixture/NuGet.Config
dotnet build dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence/cfg-probe/CfgProbe.csproj --no-restore -p:UseAppHost=false
dotnet dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence/cfg-probe/bin/Debug/net10.0/CfgProbe.dll
```

Observed values:

| Fixture | raw CFG `E-N+2P` | pinned Sonar CC |
|---|---:|---:|
| straight | 1 | 1 |
| one `if` | 2 | 2 |
| one ternary | 2 | 2 |
| `if (a && b)` | 3 | 3 |
| `try/finally` | 2 | 1 |
| one catch | 1 | 1 |
| two catches | 1 | 1 |
| filtered catch | 1 | 1 |
| two cases plus discard switch expression | 3 | 4 |
| `a || b` | 2 | 2 |
| `a ?? b` | 2 | 2 |
| `s?.Length ?? 0` | 3 | 3 |
| `value ??= ""` | 2 | 2 |
| for + foreach + while + do | 7 (compiler finally component) | 5 |
| relational `and` plus `or` pattern | 1 | 3 |
| two cases plus discard and one guard | 4 | 4 |

Calling the official metric on the enclosing `Nested` method counts both the method's `if` and a descendant lambda's ternary (CC3), while calling it on the lambda node alone returns baseline 1. This proves the production producer must use the same explicit decision-node convention but stop at nested callable roots, then give each nested callable its own baseline and count. It also disproves raw CFG complexity as the source convention: exception/finally regions, switch arms and pattern decisions do not map to a stable unadjusted edge formula.

The first invocation failed before compilation because the sandbox could not read the user NuGet configuration. A fixture-local `APPDATA` and the retained library-packs-only `NuGet.Config` made the no-network restore succeed. A later timed invocation left two fixture-owned `CfgProbe` processes; both exact PIDs were stopped before the successful build/run, and no probe process remained.
