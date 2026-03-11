namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class MessageCommand
{
    public static Command Create()
    {
        var toOption = new Option<string>("--to")
        {
            Description = "Target agent name",
            Required = true
        };

        var bodyOption = new Option<string?>("--body")
        {
            Description = "Message content"
        };

        var bodyFileOption = new Option<string?>("--body-file")
        {
            Description = "Read body from file"
        };

        var subjectOption = new Option<string?>("--subject")
        {
            Description = "Topic/task identifier"
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Allow sending to inactive agents"
        };

        var command = new Command("message", "Send a message to another agent");
        command.Aliases.Add("msg");
        command.Options.Add(toOption);
        command.Options.Add(bodyOption);
        command.Options.Add(bodyFileOption);
        command.Options.Add(subjectOption);
        command.Options.Add(forceOption);

        command.SetAction(parseResult =>
        {
            var to = parseResult.GetValue(toOption)!;
            var body = parseResult.GetValue(bodyOption);
            var bodyFile = parseResult.GetValue(bodyFileOption);
            var subject = parseResult.GetValue(subjectOption);
            var force = parseResult.GetValue(forceOption);

            var bodyFromFile = false;
            if (!string.IsNullOrEmpty(bodyFile))
            {
                if (!File.Exists(bodyFile))
                {
                    ConsoleOutput.WriteError($"Body file not found: {bodyFile}");
                    return ExitCodes.ToolError;
                }
                body = File.ReadAllText(bodyFile).Trim();
                bodyFromFile = true;
            }

            if (string.IsNullOrEmpty(body))
            {
                ConsoleOutput.WriteError("Provide --body or --body-file.");
                return ExitCodes.ToolError;
            }

            if (!bodyFromFile)
            {
                var shellMetaError = DispatchCommand.DetectShellMetacharacters(body);
                if (shellMetaError != null)
                {
                    ConsoleOutput.WriteError(shellMetaError);
                    return ExitCodes.ToolError;
                }
            }

            return MessageService.Execute(to, body, subject, force);
        });

        return command;
    }
}
