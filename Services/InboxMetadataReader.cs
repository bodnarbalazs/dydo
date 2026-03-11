namespace DynaDocs.Services;

using DynaDocs.Utils;

public class InboxMetadataReader
{
    private readonly Func<string, string> _getAgentWorkspace;

    public InboxMetadataReader(Func<string, string> getAgentWorkspace)
    {
        _getAgentWorkspace = getAgentWorkspace;
    }

    public string? GetDispatchedRole(string agentName, string task) =>
        ReadFrontmatterField(agentName, task, "role");

    public string? GetDispatchedFrom(string agentName, string task) =>
        ReadFrontmatterField(agentName, task, "from");

    private string? ReadFrontmatterField(string agentName, string task, string fieldName)
    {
        var inboxPath = Path.Combine(_getAgentWorkspace(agentName), "inbox");
        if (!Directory.Exists(inboxPath)) return null;

        var sanitizedTask = PathUtils.SanitizeForFilename(task);
        var files = Directory.GetFiles(inboxPath, $"*-{sanitizedTask}.md");
        if (files.Length == 0) return null;

        try
        {
            var content = File.ReadAllText(files[0]);
            if (!content.StartsWith("---")) return null;

            var endIndex = content.IndexOf("---", 3);
            if (endIndex < 0) return null;

            var yaml = content[3..endIndex];
            foreach (var line in yaml.Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var key = line[..colonIndex].Trim();
                if (key == fieldName)
                    return line[(colonIndex + 1)..].Trim();
            }
        }
        catch
        {
            // Malformed inbox file — don't block role setting
        }

        return null;
    }
}
