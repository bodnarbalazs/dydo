namespace DynaDocs.Services.Statechart;

internal sealed record StatechartHome(string Assets, string Dependencies, string State)
{
    public static StatechartHome Current()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cache = OperatingSystem.IsWindows() ? local : Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? Path.Combine(home, ".cache");
        var data = OperatingSystem.IsWindows() ? local : Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share");
        var state = OperatingSystem.IsWindows() ? local : Environment.GetEnvironmentVariable("XDG_STATE_HOME") ?? Path.Combine(home, ".local", "state");
        return new StatechartHome(
            StatechartPaths.Root("DYDO_STATECHART_ASSET_ROOT", Path.Combine(cache, "dydo", "statechart", "assets")),
            StatechartPaths.Root("DYDO_STATECHART_DEPENDENCY_ROOT", Path.Combine(data, "dydo", "statechart", "deps")),
            StatechartPaths.Root("DYDO_STATECHART_STATE_ROOT", Path.Combine(state, "dydo", "statechart", "state")));
    }
}
