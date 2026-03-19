namespace DynaDocs.Commands;

using DynaDocs.Services;
using DynaDocs.Utils;

internal static class TaskCompactHandler
{
    public static int Execute()
    {
        var configService = new ConfigService();
        return RunCompaction(configService);
    }

    internal static int RunCompaction(ConfigService configService)
    {
        try
        {
            var currentYearDir = Path.Combine(configService.GetAuditPath(), DateTime.UtcNow.ToString("yyyy"));
            if (!Directory.Exists(currentYearDir))
            {
                Console.WriteLine("Nothing to compact.");
                return ExitCodes.Success;
            }

            var compactionResult = SnapshotCompactionService.Compact(currentYearDir);
            if (compactionResult.SessionsProcessed > 0)
            {
                Console.WriteLine($"Audit snapshots compacted: {compactionResult.SessionsProcessed} sessions, {compactionResult.CompressionRatio:P0} reduction.");
                ResetCounter(configService);
            }
            else
            {
                Console.WriteLine("Nothing to compact.");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Compaction failed: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static void ResetCounter(ConfigService configService)
    {
        try
        {
            var counterPath = GetCounterPath(configService);
            if (File.Exists(counterPath))
                File.WriteAllText(counterPath, "0");
        }
        catch
        {
            // Counter reset failure is non-critical
        }
    }

    internal static string GetCounterPath(ConfigService configService)
    {
        var systemPath = Path.Combine(configService.GetDocsPath(), "_system");
        return Path.Combine(systemPath, "compact-counter");
    }
}
