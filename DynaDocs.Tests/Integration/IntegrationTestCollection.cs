namespace DynaDocs.Tests.Integration;

/// <summary>
/// Collection definition for integration tests.
/// Tests in this collection run sequentially (not in parallel) because they
/// share process-global state: Environment.CurrentDirectory and Console.Out/Error.
/// </summary>
[CollectionDefinition("Integration", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<object>
{
    // This class has no code - it's just a marker for xUnit
}
