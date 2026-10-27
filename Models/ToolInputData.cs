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
    /// The shell command line. Codex may send it as an argv array instead: a shell running one
    /// script (<c>["bash", "-lc", script]</c>, <c>[pwsh, "-Command", script]</c>) reads as that
    /// script, any other array as its words joined into one POSIX line.
    /// </summary>
    [JsonIgnore]
    public string? Command { get; set; }

    [JsonPropertyName("command")]
    public JsonElement? CommandValue
    {
        get => Command == null ? null : StringElement(Command);
        set => Command = value?.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Array => ScriptOf([.. value.Value.EnumerateArray().Select(WordOf)]),
            _ => null,
        };
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

    private static readonly HashSet<string> Shells =
        new(StringComparer.OrdinalIgnoreCase) { "bash", "sh", "zsh", "dash", "ksh", "pwsh", "powershell", "cmd" };

    private static readonly HashSet<string> ScriptFlags =
        new(StringComparer.OrdinalIgnoreCase) { "-c", "-lc", "-ic", "-Command", "/c" };

    private static JsonElement StringElement(string text)
    {
        using var document = JsonDocument.Parse($"\"{JsonEncodedText.Encode(text)}\"");
        return document.RootElement.Clone();
    }

    private static string WordOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();

    private static string ScriptOf(string[] argv)
    {
        var shell = System.IO.Path.GetFileNameWithoutExtension(argv.FirstOrDefault()?.Replace('\\', '/'));
        if (argv.Length >= 3 && ScriptFlags.Contains(argv[^2]) && Shells.Contains(shell ?? ""))
            return argv[^1];
        return string.Join(' ', argv.Select(Quoted));
    }

    private static string Quoted(string word) =>
        word.Length > 0 && word.All(c => char.IsLetterOrDigit(c) || "-_./:=@,+%".Contains(c))
            ? word
            : $"'{word.Replace("'", "'\\''")}'";
}
