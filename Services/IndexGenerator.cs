namespace DynaDocs.Services;

using System.Text;
using DynaDocs.Models;

public class IndexGenerator : IIndexGenerator
{
    public string Generate(List<DocFile> docs, string basePath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.AppendLine("area: general");
        sb.AppendLine("type: hub");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# DynaDocs Index");
        sb.AppendLine();
        sb.AppendLine("This is the entry point for AI agents and humans exploring the DynaDocs documentation.");
        sb.AppendLine();
        sb.AppendLine("## How to Navigate");
        sb.AppendLine();
        sb.AppendLine("1. Start with [About This Project](./understand/about.md) if you're new");
        sb.AppendLine("2. Browse by purpose below");
        sb.AppendLine("3. Use the [Glossary](./glossary.md) for term definitions");
        sb.AppendLine();
        sb.AppendLine("## Documentation Sections");
        sb.AppendLine();

        var topLevelHubs = new (string folder, string heading, string linkText, string description)[]
        {
            ("understand", "Understand - What Things Are", "Understanding the Platform", "Core concepts, domain knowledge, architecture"),
            ("guides", "Guides - How to Do Things", "Development Guides", "Task-oriented guides for backend, frontend, microservices"),
            ("reference", "Reference - Specs and Lookups", "Reference Documentation", "API specs, configuration, tool documentation"),
            ("project", "Project - How We Work", "Project Meta", "Decisions, pitfalls, changelog, docs system")
        };

        foreach (var (folder, heading, linkText, description) in topLevelHubs)
        {
            var sectionPath = $"./{folder}/_{folder}.md";
            var sectionExists = docs.Any(d => d.RelativePath.Equals($"{folder}/_{folder}.md", StringComparison.OrdinalIgnoreCase));
            var folderExists = docs.Any(d => d.RelativePath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));

            sb.AppendLine($"### {heading}");
            if (sectionExists)
            {
                sb.AppendLine($"[{linkText}]({sectionPath}) - {description}");
            }
            else if (folderExists)
            {
                sb.AppendLine(description);
            }
            else
            {
                sb.AppendLine($"*{folder}/ folder not found*");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
