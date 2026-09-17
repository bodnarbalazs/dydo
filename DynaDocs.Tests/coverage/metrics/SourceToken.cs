namespace GateMetrics;

public sealed record SourceToken(string Kind, string Text, int Line, int Column, int EndLine, int EndColumn);
