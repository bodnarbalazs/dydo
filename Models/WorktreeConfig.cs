namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class WorktreeConfig
{
    [JsonPropertyName("mergeSafety")]
    public MergeSafetyConfig MergeSafety { get; set; } = new();
}
