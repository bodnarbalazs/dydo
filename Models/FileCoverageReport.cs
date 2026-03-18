namespace DynaDocs.Models;

public record FileCoverageReport(
    DateTime Generated,
    int SinceDays,
    List<FolderCoverage> Folders,
    int TotalFiles,
    int CoveredCount,
    int LowCount,
    int GapCount,
    int StaleCount
);
