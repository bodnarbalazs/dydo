namespace DynaDocs.Models;

public record DispatchOptions(
    string Role,
    string Task,
    string Brief,
    string? Files = null,
    string? To = null,
    string? Queue = null,
    bool NoLaunch = false,
    bool Escalate = false,
    bool UseTab = false,
    bool UseNewWindow = false,
    bool AutoClose = false,
    bool Wait = false,
    bool Worktree = false
);
