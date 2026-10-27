namespace DynaDocs.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

public class ToolInputData
{
    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("old_string")]
    public string? OldString { get; set; }

    [JsonPropertyName("new_string")]
    public string? NewString { get; set; }

    /// <summary>
    /// The shell command line. Codex may send it as an argv array instead, which reads as its words
    /// joined into one POSIX line; <see cref="CommandArgv"/> keeps the array, and the guard reads a
    /// shell envelope in it as that script.
    /// </summary>
    [JsonIgnore]
    public string? Command { get; set; }

    /// <summary>The command as Codex sent it when it sent an argv array, else null.</summary>
    [JsonIgnore]
    internal IReadOnlyList<string>? CommandArgv { get; private set; }

    [JsonPropertyName("command")]
    public JsonElement? CommandValue
    {
        set
        {
            CommandArgv = value?.ValueKind == JsonValueKind.Array ? [.. value.Value.EnumerateArray().Select(WordOf)] : null;
            if (CommandArgv != null)
                Command = string.Join(' ', CommandArgv.Select(Quoted));
            else
                Command = value?.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        }
    }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>File path of a NotebookEdit tool call (its analogue of file_path).</summary>
    [JsonPropertyName("notebook_path")]
    public string? NotebookPath { get; set; }

    [JsonPropertyName("run_in_background")]
    public bool? RunInBackground { get; set; }

    /// <summary>The directory a shell call runs in, when the host names one per call.</summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    /// <summary>Codex's name for the directory a shell call runs in.</summary>
    [JsonPropertyName("workdir")]
    public string? Workdir { get; set; }

    private static string WordOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();

    private static string Quoted(string word) =>
        word.Length > 0 && word.All(c => char.IsLetterOrDigit(c) || "-_./:=@,+%".Contains(c))
            ? word
            : $"'{word.Replace("'", "'\\''")}'";
}
