namespace GateMetrics;

public sealed record SourceMember(
    string Id, string Kind, string Member, string Type, int Line, int Column,
    int EndLine, int EndColumn, int SpanStart, int SpanLength,
    int Cognitive, int Parameters, bool Constructor);
