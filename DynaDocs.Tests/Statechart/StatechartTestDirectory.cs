namespace DynaDocs.Tests.Statechart;

internal sealed class StatechartTestDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("statechart-test-").FullName;
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
