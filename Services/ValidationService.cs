namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;

public class ValidationService : IValidationService
{
    public List<ValidationIssue> ValidateSystem(string basePath)
    {
        var issues = new List<ValidationIssue>();

        ValidateDydoJson(basePath, issues);

        return issues;
    }

    private static void ValidateDydoJson(string basePath, List<ValidationIssue> issues)
    {
        var configPath = Path.Combine(basePath, "dydo.json");
        if (!File.Exists(configPath))
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = "dydo.json",
                Message = "dydo.json not found."
            });
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig);
            if (config == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    File = "dydo.json",
                    Message = "Failed to deserialize dydo.json."
                });
            }
            else
            {
                ValidateNudges(config, issues);
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = "dydo.json",
                Message = $"Invalid JSON: {ex.Message}"
            });
        }
    }

    private static void ValidateNudges(DydoConfig config, List<ValidationIssue> issues)
    {
        for (int i = 0; i < config.Nudges.Count; i++)
        {
            var nudge = config.Nudges[i];

            if (string.IsNullOrWhiteSpace(nudge.Pattern))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error", File = "dydo.json",
                    Message = $"Nudge [{i}] has empty pattern."
                });
                continue;
            }

            // Tool-scoped nudges (Decision 026 §4) carry glob patterns, not regexes.
            if (nudge.Tools is not { Count: > 0 })
            {
                try { _ = new Regex(nudge.Pattern); }
                catch (ArgumentException ex)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "error", File = "dydo.json",
                        Message = $"Nudge [{i}] has invalid regex pattern: {ex.Message}"
                    });
                }
            }

            if (string.IsNullOrWhiteSpace(nudge.Message))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error", File = "dydo.json",
                    Message = $"Nudge [{i}] has empty message."
                });
            }

            if (!string.Equals(nudge.Severity, "block", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(nudge.Severity, "warn", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(nudge.Severity, "notice", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error", File = "dydo.json",
                    Message = $"Nudge [{i}] has invalid severity '{nudge.Severity}'. Must be 'block', 'warn', or 'notice'."
                });
            }
        }
    }

}
