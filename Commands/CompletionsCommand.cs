namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Utils;

public static class CompletionsCommand
{
    private const string BashScript = """
        _dydo_completions() {
            local cur_word="${COMP_WORDS[COMP_CWORD]}"
            local completions
            completions=$(dydo _complete "$COMP_CWORD" "${COMP_WORDS[@]}" 2>/dev/null)
            if [ $? -eq 0 ]; then
                COMPREPLY=($(compgen -W "$completions" -- "$cur_word"))
            fi
        }
        complete -F _dydo_completions dydo
        """;

    private const string ZshScript = """
        _dydo_completions() {
            local completions
            completions=("${(@f)$(dydo _complete "$((CURRENT-1))" "${words[@]}" 2>/dev/null)}")
            if [ ${#completions[@]} -gt 0 ]; then
                compadd -a completions
            fi
        }
        compdef _dydo_completions dydo
        """;

    private const string PowerShellScript = """
        Register-ArgumentCompleter -CommandName dydo -Native -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)
            $cmdText = $commandAst.ToString().Substring(0, $cursorPosition)
            $words = $cmdText -split '\s+'
            $position = $words.Count - 1
            if ($cmdText[-1] -eq ' ') { $position++ }
            $completions = & dydo _complete $position @words 2>$null
            if ($LASTEXITCODE -eq 0 -and $completions) {
                $completions -split "`n" | Where-Object { $_.Trim() } | ForEach-Object {
                    $text = $_.Trim()
                    if ($text -like "$wordToComplete*") {
                        [System.Management.Automation.CompletionResult]::new($text, $text, 'ParameterValue', $text)
                    }
                }
            }
        }
        """;

    public static Command Create()
    {
        var shellArgument = new Argument<string>("shell")
        {
            Description = "Shell type (bash, zsh, powershell)"
        };

        var command = new Command("completions", "Output shell completion script");
        command.Arguments.Add(shellArgument);

        command.SetAction(parseResult =>
        {
            var shell = parseResult.GetValue(shellArgument)!;
            return Execute(shell);
        });

        return command;
    }

    private static int Execute(string shell)
    {
        var script = shell.ToLowerInvariant() switch
        {
            "bash" => BashScript,
            "zsh" => ZshScript,
            "powershell" or "pwsh" => PowerShellScript,
            _ => null
        };

        if (script == null)
        {
            ConsoleOutput.WriteError($"Unknown shell: {shell}. Supported: bash, zsh, powershell");
            return ExitCodes.ToolError;
        }

        Console.WriteLine(script);
        return ExitCodes.Success;
    }

}
