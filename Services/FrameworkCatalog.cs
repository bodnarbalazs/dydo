namespace DynaDocs.Services;

using System.Security.Cryptography;
using System.Text;

internal static class FrameworkCatalog
{
    internal static readonly string[] DocumentFiles =
    [
        "reference/about-dynadocs.md",
        "reference/dydo-commands.md",
        "reference/dydo-glossary.md",
        "reference/linear-workspace-standard.md",
        "reference/writing-docs.md",
        "guides/working-tree-contract.md"
    ];

    internal static readonly string[] RetiredSkills =
        ["sprint-auditor", "orchestrator", "manager", "planner", "test-writer", "code-writer", "issue-planner"];

    internal static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(content)));
        return Convert.ToHexStringLower(bytes);
    }

    internal static string Normalize(string content)
    {
        if (content.Length > 0 && content[0] == '\uFEFF')
            content = content[1..];

        return content.Replace("\r\n", "\n");
    }

    internal static string ComputeHashBytes(byte[] content)
    {
        var bytes = SHA256.HashData(content);
        return Convert.ToHexStringLower(bytes);
    }
}
