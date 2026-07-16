namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Minimal read-only accessor over the project config that the surviving guard needs: the loaded
/// <see cref="DydoConfig"/> (for nudges) and the agents-root path (where the guard writes its
/// warn-nudge pass-through markers). The claim / roster / identity / session / wait / message /
/// resume / agent-state machinery was carved out across the 2.1.0 simplification campaign
/// (DR-041): identity is assigned at spawn now, not claimed, so nothing writes or reads agent
/// state at runtime.
/// </summary>
public class AgentRegistry
{
    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly DydoConfig? _config;

    public AgentRegistry(string? basePath = null, IConfigService? configService = null)
    {
        _basePath = basePath ?? PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
        _configService = configService ?? new ConfigService();
        _config = _configService.LoadConfig(_basePath);
    }

    public DydoConfig? Config => _config;

    public string WorkspacePath =>
        _configService.GetAgentsPath(_basePath);
}
