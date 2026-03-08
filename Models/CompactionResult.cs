namespace DynaDocs.Models;

public class CompactionResult
{
    public int SessionsProcessed { get; set; }
    public long OldTotalSizeBytes { get; set; }
    public long NewTotalSizeBytes { get; set; }
    public long NewBaselineSizeBytes { get; set; }
    public int OldBaselinesRemoved { get; set; }
    public int UniqueCommits { get; set; }

    public double CompressionRatio =>
        OldTotalSizeBytes > 0 ? 1.0 - (double)NewTotalSizeBytes / OldTotalSizeBytes : 0;
}
