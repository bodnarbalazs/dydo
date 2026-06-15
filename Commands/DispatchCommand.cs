namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class DispatchCommand
{
    public static Command Create()
    {
        var roleOption = new Option<string>("--role")
        {
            Description = "Role for the target agent",
            Required = true
        };

        var taskOption = new Option<string>("--task")
        {
            Description = "Task name",
            Required = true
        };

        var briefOption = new Option<string?>("--brief")
        {
            Description = "Brief description of the work"
        };

        var briefFileOption = new Option<string?>("--brief-file")
        {
            Description = "Path to a file containing the brief"
        };

        var filesOption = new Option<string?>("--files")
        {
            Description = "File pattern to include (e.g., 'src/Auth/**')"
        };

        var noLaunchOption = new Option<bool>("--no-launch")
        {
            Description = "Don't launch new terminal, just write to inbox"
        };

        var tabOption = new Option<bool>("--tab")
        {
            Description = "Launch in a new tab (overrides config)"
        };

        var newWindowOption = new Option<bool>("--new-window")
        {
            Description = "Launch in a new window (overrides config)"
        };

        var toOption = new Option<string?>("--to")
        {
            Description = "Send dispatch to specific agent (skips auto-selection)"
        };
        toOption.Aliases.Add("--agent");

        var escalateOption = new Option<bool>("--escalate")
        {
            Description = "Mark dispatch as escalated after repeated failures"
        };

        var autoCloseOption = new Option<bool>("--auto-close")
        {
            Description = "Auto-close the dispatched agent's terminal after release"
        };

        var command = new Command("dispatch", "Dispatch work to another agent");
        command.Options.Add(roleOption);
        command.Options.Add(taskOption);
        command.Options.Add(briefOption);
        command.Options.Add(briefFileOption);
        command.Options.Add(filesOption);
        command.Options.Add(noLaunchOption);
        command.Options.Add(toOption);
        command.Options.Add(escalateOption);
        command.Options.Add(autoCloseOption);
        command.Options.Add(tabOption);
        command.Options.Add(newWindowOption);

        command.SetAction(parseResult =>
        {
            var role = parseResult.GetValue(roleOption)!;
            var task = parseResult.GetValue(taskOption)!;
            var brief = parseResult.GetValue(briefOption);
            var briefFile = parseResult.GetValue(briefFileOption);
            var files = parseResult.GetValue(filesOption);
            var noLaunch = parseResult.GetValue(noLaunchOption);
            var to = parseResult.GetValue(toOption);
            var escalate = parseResult.GetValue(escalateOption);
            var useTab = parseResult.GetValue(tabOption);
            var useNewWindow = parseResult.GetValue(newWindowOption);
            var autoClose = parseResult.GetValue(autoCloseOption);

            var briefFromFile = false;
            if (!string.IsNullOrEmpty(briefFile))
            {
                if (!File.Exists(briefFile))
                {
                    ConsoleOutput.WriteError($"Brief file not found: {briefFile}");
                    return ExitCodes.ToolError;
                }
                brief = File.ReadAllText(briefFile).Trim();
                briefFromFile = true;
            }

            if (string.IsNullOrEmpty(brief))
            {
                ConsoleOutput.WriteError("Provide --brief or --brief-file.");
                return ExitCodes.ToolError;
            }

            if (!briefFromFile)
            {
                var shellMetaError = DetectShellMetacharacters(brief);
                if (shellMetaError != null)
                {
                    ConsoleOutput.WriteError(shellMetaError);
                    return ExitCodes.ToolError;
                }
            }

            return DispatchService.Execute(new DispatchOptions(role, task, brief, files, to, noLaunch, escalate, useTab, useNewWindow, autoClose));
        });

        return command;
    }

    /// <summary>
    /// Checks a brief for shell metacharacters that indicate the Bash command was garbled
    /// (e.g. unquoted &&, ||, $()). Returns an error message if detected, null if clean.
    /// Briefs loaded via --brief-file skip this check since they bypass the shell.
    /// </summary>
    internal static string? DetectShellMetacharacters(string brief)
    {
        // Patterns that almost certainly indicate shell garbling, not intentional prose
        string[] shellPatterns = ["&&", "||", "$(", "${", "`"];

        foreach (var pattern in shellPatterns)
        {
            if (brief.Contains(pattern))
                return $"Brief contains shell metacharacter '{pattern}'. " +
                       "This usually means the --brief value was not properly quoted in the shell. " +
                       "Use --brief-file instead for complex content.";
        }

        return null;
    }
}
