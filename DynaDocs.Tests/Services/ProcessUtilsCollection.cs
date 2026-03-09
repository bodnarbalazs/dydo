namespace DynaDocs.Tests.Services;

/// <summary>
/// Disables xUnit parallel execution between test classes that share the
/// static mutable ProcessUtils.PowerShellResolverOverride field.
/// </summary>
[CollectionDefinition("ProcessUtils")]
public class ProcessUtilsCollection;
