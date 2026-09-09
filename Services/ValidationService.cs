namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;

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
            var config = new ConfigService().LoadConfigStrict(basePath)!;
            ValidateNudges(config, issues);
        }
        catch (InvalidDataException ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = "dydo.json",
                Message = ex.Message
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

            if (nudge.Audience is not ("all" or "manager" or "worker"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error", File = "dydo.json",
                    Message = $"Nudge [{i}] has invalid audience '{nudge.Audience}'. Must be 'all', 'manager', or 'worker'."
                });
            }

            try { _ = new Regex(nudge.Pattern); }
            catch (ArgumentException ex)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error", File = "dydo.json",
                    Message = $"Nudge [{i}] has invalid regex pattern: {ex.Message}"
                });
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
