namespace GateMetrics;

public sealed record CompiledMethod(int Token, string Identity, string Key, string? Kickoff, string? KickoffKey, bool Generated,
    IReadOnlyList<SourcePoint> Points);
