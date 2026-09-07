namespace GateMetrics;

public sealed record FileFacts(string Path, IReadOnlyList<SourceMember> Methods);
