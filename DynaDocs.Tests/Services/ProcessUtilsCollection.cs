namespace DynaDocs.Tests.Services;

/// <summary>
/// Disables xUnit parallel execution between test classes that share
/// static mutable ProcessUtils overrides (IsProcessRunningOverride,
/// GetProcessNameOverride, PowerShellResolverOverride).
/// </summary>
[CollectionDefinition("ProcessUtils")]
public class ProcessUtilsCollection;
