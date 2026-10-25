namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class CleanConfig
{
    public const int DefaultArchiveSizeCapMb = 1024;

    /// <summary>The clean archive's size cap in megabytes; past it the oldest entries are pruned.</summary>
    [JsonPropertyName("archiveSizeCapMb")]
    public int? ArchiveSizeCapMb { get; set; }
}
