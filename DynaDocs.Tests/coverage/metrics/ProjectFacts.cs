namespace GateMetrics;

public sealed record ProjectFacts(bool HasCompilation, string Project, string? Assembly,
    IReadOnlyList<FileFacts> Files, IReadOnlyList<string[]> NamespaceEdges, IReadOnlyList<string> GeneratedFiles,
    BehaviorFacts Behavior);
