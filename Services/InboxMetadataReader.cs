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
                var fields = FrontmatterParser.ParseFields(content);
                if (fields == null) continue;

                fields.TryGetValue(fieldName, out var fieldValue);
                var received = DateTime.MinValue;
                if (fields.TryGetValue("received", out var receivedStr))
                    DateTime.TryParse(receivedStr, out received);

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
