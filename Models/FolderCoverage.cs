namespace DynaDocs.Models;

public record FolderCoverage(
    string Path,
    List<FileCoverageEntry> Files,
    List<FolderCoverage> SubFolders,
    int TotalFiles,
    int CoveredCount,
    double AverageScore
);
