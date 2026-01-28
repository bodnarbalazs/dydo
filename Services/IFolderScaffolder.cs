namespace DynaDocs.Services;

public interface IFolderScaffolder
{
    /// <summary>
    /// Scaffold with default agent names (Set1).
    /// </summary>
    void Scaffold(string basePath);

    /// <summary>
    /// Scaffold with custom agent names.
    /// </summary>
    void Scaffold(string basePath, List<string> agentNames);
}
