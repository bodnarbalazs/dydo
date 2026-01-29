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

    /// <summary>
    /// Create a single agent's workspace with workflow.md and mode files.
    /// </summary>
    void ScaffoldAgentWorkspace(string agentsPath, string agentName);

    /// <summary>
    /// Regenerate workflow and mode files for an agent (used after rename).
    /// </summary>
    void RegenerateAgentFiles(string agentsPath, string agentName);
}
