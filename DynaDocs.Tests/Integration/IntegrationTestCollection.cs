namespace DynaDocs.Tests.Integration;

/// <summary>
/// Tests in this collection run sequentially because they share process-global state:
/// Environment.CurrentDirectory and Console.Out/Error.
/// </summary>
[CollectionDefinition("Integration", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<object>
{
}
