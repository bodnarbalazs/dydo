namespace DynaDocs.Tests.Services;

/// <summary>
/// Tag for test classes that share static mutable ProcessUtils overrides
/// (IsProcessRunningOverride, GetProcessNameOverride, PowerShellResolverOverride).
/// Assembly-wide DisableTestParallelization (AssemblyInfo.cs) handles the actual
/// serialisation; this collection remains as documentation and as a future enabler
/// if assembly-wide parallelism is ever lifted.
/// </summary>
[CollectionDefinition("ProcessUtils")]
public class ProcessUtilsCollection;
