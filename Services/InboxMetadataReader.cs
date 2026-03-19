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

    public string? GetDispatchedFromRole(string agentName, string task) =>
        ReadFrontmatterField(agentName, task, "from_role");

    private string? ReadFrontmatterField(string agentName, string task, string fieldName)
    {
        var inboxPath = Path.Combine(_getAgentWorkspace(agentName), "inbox");
        if (!Directory.Exists(inboxPath)) return null;

        var sanitizedTask = PathUtils.SanitizeForFilename(task);
        var files = Directory.GetFiles(inboxPath, $"*-{sanitizedTask}.md");
        if (files.Length == 0) return null;

        string? bestValue = null;
        var bestReceived = DateTime.MinValue;

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (!content.StartsWith("---")) continue;

                var endIndex = content.IndexOf("---", 3);
                if (endIndex < 0) continue;

                var yaml = content[3..endIndex];
                string? fieldValue = null;
                var received = DateTime.MinValue;

                foreach (var line in yaml.Split('\n'))
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex < 0) continue;

                    var key = line[..colonIndex].Trim();
                    if (key == fieldName)
                        fieldValue = line[(colonIndex + 1)..].Trim();
                    else if (key == "received" && DateTime.TryParse(line[(colonIndex + 1)..].Trim(), out var dt))
                        received = dt;
                }

                if (fieldValue != null && received >= bestReceived)
                {
                    bestReceived = received;
                    bestValue = fieldValue;
                }
            }
            catch
            {
                // Malformed inbox file — skip
            }
        }

        return bestValue;
    }
}
