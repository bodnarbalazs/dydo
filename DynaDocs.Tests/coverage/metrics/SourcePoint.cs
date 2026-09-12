namespace GateMetrics;

public sealed record SourcePoint(string Path, string Origin, int Offset, int Line, int Column, int EndLine, int EndColumn,
    string ChecksumAlgorithm, string Checksum);
