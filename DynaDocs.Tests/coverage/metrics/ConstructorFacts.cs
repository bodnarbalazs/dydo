namespace GateMetrics;

public sealed record ConstructorFacts(string Key, int Cognitive, int PolicyCc, int Parameters,
    IReadOnlyList<string> Fragments);
