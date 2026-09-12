namespace GateMetrics;

public sealed record BehaviorFragment(string Id, string Path, string Kind, int Line, int Column,
    int EndLine, int EndColumn, IReadOnlyList<string> Owners);
