namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IConfigService
{
    string? FindConfigFile(string? startPath = null);
    DydoConfig? LoadConfig(string? startPath = null);
    void SaveConfig(DydoConfig config, string path);
    string? GetHumanFromEnv();
    string? GetProjectRoot(string? startPath = null);
    string GetDydoRoot(string? startPath = null);
    string GetAgentsPath(string? startPath = null);
    string GetDocsPath(string? startPath = null);
    string GetTasksPath(string? startPath = null);
    string GetAuditPath(string? startPath = null);
    string GetChangelogPath(string? startPath = null);
    (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config);
}
