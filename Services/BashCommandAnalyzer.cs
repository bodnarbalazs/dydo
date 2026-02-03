namespace DynaDocs.Services;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Analyzes shell commands to detect file operations.
/// Supports bash, zsh, PowerShell, and Windows cmd.
/// </summary>
public partial class BashCommandAnalyzer : IBashCommandAnalyzer
{
    // Read commands by shell type
    private static readonly Dictionary<string, FileOperationType> ReadCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix/Linux/macOS
        ["cat"] = FileOperationType.Read,
        ["head"] = FileOperationType.Read,
        ["tail"] = FileOperationType.Read,
        ["less"] = FileOperationType.Read,
        ["more"] = FileOperationType.Read,
        ["grep"] = FileOperationType.Read,
        ["egrep"] = FileOperationType.Read,
        ["fgrep"] = FileOperationType.Read,
        ["rg"] = FileOperationType.Read,         // ripgrep
        ["ag"] = FileOperationType.Read,         // silver searcher
        ["ack"] = FileOperationType.Read,
        ["strings"] = FileOperationType.Read,
        ["xxd"] = FileOperationType.Read,
        ["hexdump"] = FileOperationType.Read,
        ["od"] = FileOperationType.Read,
        ["file"] = FileOperationType.Read,
        ["wc"] = FileOperationType.Read,
        ["source"] = FileOperationType.Read,
        ["."] = FileOperationType.Read,          // dot command

        // Windows cmd
        ["type"] = FileOperationType.Read,
        ["find"] = FileOperationType.Read,       // Windows find
        ["findstr"] = FileOperationType.Read,

        // PowerShell
        ["get-content"] = FileOperationType.Read,
        ["gc"] = FileOperationType.Read,         // Alias
        ["select-string"] = FileOperationType.Read,
        ["import-csv"] = FileOperationType.Read,
        ["import-clixml"] = FileOperationType.Read,
        ["convertfrom-json"] = FileOperationType.Read,
    };

    private static readonly Dictionary<string, FileOperationType> WriteCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix/Linux/macOS
        ["tee"] = FileOperationType.Write,
        ["dd"] = FileOperationType.Write,
        ["install"] = FileOperationType.Write,
        ["touch"] = FileOperationType.Write,
        ["truncate"] = FileOperationType.Write,
        ["mkfifo"] = FileOperationType.Write,
        ["mknod"] = FileOperationType.Write,
        ["patch"] = FileOperationType.Write,
        ["split"] = FileOperationType.Write,
        ["csplit"] = FileOperationType.Write,

        // Windows cmd
        ["copy"] = FileOperationType.Copy,
        ["xcopy"] = FileOperationType.Copy,
        ["robocopy"] = FileOperationType.Copy,
        ["move"] = FileOperationType.Move,
        ["ren"] = FileOperationType.Move,
        ["rename"] = FileOperationType.Move,

        // PowerShell
        ["set-content"] = FileOperationType.Write,
        ["sc"] = FileOperationType.Write,        // Note: sc is also Windows service control
        ["out-file"] = FileOperationType.Write,
        ["add-content"] = FileOperationType.Write,
        ["ac"] = FileOperationType.Write,        // Alias
        ["new-item"] = FileOperationType.Write,
        ["ni"] = FileOperationType.Write,        // Alias
        ["copy-item"] = FileOperationType.Copy,
        ["ci"] = FileOperationType.Copy,         // Alias
        ["cpi"] = FileOperationType.Copy,        // Alias
        ["move-item"] = FileOperationType.Move,
        ["mi"] = FileOperationType.Move,         // Alias
        ["export-csv"] = FileOperationType.Write,
        ["export-clixml"] = FileOperationType.Write,
        ["convertto-json"] = FileOperationType.Write,
    };

    private static readonly Dictionary<string, FileOperationType> DeleteCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix/Linux/macOS
        ["rm"] = FileOperationType.Delete,
        ["rmdir"] = FileOperationType.Delete,
        ["unlink"] = FileOperationType.Delete,
        ["shred"] = FileOperationType.Delete,

        // Windows cmd
        ["del"] = FileOperationType.Delete,
        ["erase"] = FileOperationType.Delete,
        ["rd"] = FileOperationType.Delete,

        // PowerShell
        ["remove-item"] = FileOperationType.Delete,
        ["ri"] = FileOperationType.Delete,       // Alias
        ["clear-content"] = FileOperationType.Write, // Clears but doesn't delete
    };

    private static readonly Dictionary<string, FileOperationType> PermissionCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unix
        ["chmod"] = FileOperationType.PermissionChange,
        ["chown"] = FileOperationType.PermissionChange,
        ["chgrp"] = FileOperationType.PermissionChange,
        ["setfacl"] = FileOperationType.PermissionChange,
        ["chattr"] = FileOperationType.PermissionChange,
        ["lsattr"] = FileOperationType.Read,

        // Windows
        ["icacls"] = FileOperationType.PermissionChange,
        ["cacls"] = FileOperationType.PermissionChange,
        ["takeown"] = FileOperationType.PermissionChange,
        ["attrib"] = FileOperationType.PermissionChange,

        // PowerShell
        ["set-acl"] = FileOperationType.PermissionChange,
        ["get-acl"] = FileOperationType.Read,
    };

    private static readonly Dictionary<string, FileOperationType> CopyMoveCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cp"] = FileOperationType.Copy,
        ["mv"] = FileOperationType.Move,
        ["ln"] = FileOperationType.Write,        // Creating links
        ["rsync"] = FileOperationType.Copy,
        ["scp"] = FileOperationType.Copy,
    };

    // Dangerous patterns that should always be blocked
    private static readonly (Regex Pattern, string Reason)[] DangerousPatterns =
    [
        // Recursive delete of root or home
        (RecursiveDeleteRootRegex(), "Recursive delete of root or home directory"),
        (RecursiveDeleteGlobRegex(), "Recursive delete with dangerous glob pattern"),

        // Fork bomb patterns
        (ForkBombClassicRegex(), "Fork bomb detected"),
        (ForkBombAltRegex(), "Fork bomb variant detected"),

        // Direct disk writes
        (DirectDiskWriteRegex(), "Direct disk write attempt"),
        (DdDiskWriteRegex(), "Direct disk write via dd"),

        // Download and execute - Unix
        (CurlPipeShRegex(), "Download and execute pattern (curl | sh)"),
        (WgetPipeShRegex(), "Download and execute pattern (wget | sh)"),
        (WgetOutputPipeShRegex(), "Download and execute pattern (wget -O | sh)"),

        // Download and execute - PowerShell
        (PowerShellIwrIexRegex(), "Download and execute pattern (PowerShell IWR | IEX)"),
        (PowerShellIwrIexShortRegex(), "Download and execute pattern (PowerShell iwr | iex)"),
        (PowerShellDownloadStringRegex(), "Download and execute pattern (DownloadString | IEX)"),

        // Eval of untrusted input
        (EvalVariableRegex(), "Eval of variable content"),

        // History clearing (potential cover-up)
        (HistoryClearRegex(), "History clearing detected"),
        (HistoryFileTruncateRegex(), "History file truncation"),
        (PowerShellHistoryDeleteRegex(), "PowerShell history deletion"),

        // Disable security features
        (SelinuxDisableRegex(), "SELinux disable attempt"),
        (FirewallFlushRegex(), "Firewall flush attempt"),

        // Credential theft indicators
        (ShadowFileAccessRegex(), "Shadow file access attempt"),
        (PasswdModifyRegex(), "Password file modification attempt"),
    ];

    // Bypass detection patterns
    [GeneratedRegex(@"\$\([^)]+\)|`[^`]+`", RegexOptions.Compiled)]
    private static partial Regex CommandSubstitutionRegex();

    [GeneratedRegex(@"base64\s+(-d|--decode)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex Base64DecodeRegex();

    [GeneratedRegex(@"xxd\s+-r|od\s+-A", RegexOptions.Compiled)]
    private static partial Regex HexDecodeRegex();

    [GeneratedRegex(@"\$[A-Za-z_][A-Za-z0-9_]*|\$\{[^}]+\}", RegexOptions.Compiled)]
    private static partial Regex VariableExpansionRegex();

    // Dangerous pattern regexes (generated for performance)
    [GeneratedRegex(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+(/|~|/\*)(\s+--[a-z-]+)*\s*($|;|&&|\|\||&|\|)", RegexOptions.Compiled)]
    private static partial Regex RecursiveDeleteRootRegex();

    [GeneratedRegex(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+\*\s*($|;|&&|\|\||&|\|)", RegexOptions.Compiled)]
    private static partial Regex RecursiveDeleteGlobRegex();

    [GeneratedRegex(@":\s*\(\s*\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:", RegexOptions.Compiled)]
    private static partial Regex ForkBombClassicRegex();

    [GeneratedRegex(@"\.\s*/\s*\.:", RegexOptions.Compiled)]
    private static partial Regex ForkBombAltRegex();

    [GeneratedRegex(@">\s*/dev/sd[a-z]", RegexOptions.Compiled)]
    private static partial Regex DirectDiskWriteRegex();

    [GeneratedRegex(@"dd\s+.*of\s*=\s*/dev/sd[a-z]", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DdDiskWriteRegex();

    [GeneratedRegex(@"curl\s+[^|]*\|\s*(ba)?sh", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CurlPipeShRegex();

    [GeneratedRegex(@"wget\s+[^|]*\|\s*(ba)?sh", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex WgetPipeShRegex();

    [GeneratedRegex(@"wget\s+-O\s*-?\s+[^|]*\|\s*(ba)?sh", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex WgetOutputPipeShRegex();

    [GeneratedRegex(@"Invoke-WebRequest[^|]*\|\s*Invoke-Expression", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellIwrIexRegex();

    [GeneratedRegex(@"iwr\s+[^|]*\|\s*iex", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellIwrIexShortRegex();

    [GeneratedRegex(@"DownloadString\s*\([^)]+\)[^|]*\|\s*(iex|Invoke-Expression)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellDownloadStringRegex();

    [GeneratedRegex(@"eval\s+\$", RegexOptions.Compiled)]
    private static partial Regex EvalVariableRegex();

    [GeneratedRegex(@"history\s+-c", RegexOptions.Compiled)]
    private static partial Regex HistoryClearRegex();

    [GeneratedRegex(@">\s*~/\.bash_history|>\s*~/\.zsh_history", RegexOptions.Compiled)]
    private static partial Regex HistoryFileTruncateRegex();

    [GeneratedRegex(@"Remove-Item.*ConsoleHost_history\.txt", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellHistoryDeleteRegex();

    [GeneratedRegex(@"setenforce\s+0", RegexOptions.Compiled)]
    private static partial Regex SelinuxDisableRegex();

    [GeneratedRegex(@"iptables\s+-F", RegexOptions.Compiled)]
    private static partial Regex FirewallFlushRegex();

    [GeneratedRegex(@"cat\s+/etc/shadow|head\s+/etc/shadow|tail\s+/etc/shadow", RegexOptions.Compiled)]
    private static partial Regex ShadowFileAccessRegex();

    [GeneratedRegex(@">\s*/etc/passwd|echo.*>>\s*/etc/passwd", RegexOptions.Compiled)]
    private static partial Regex PasswdModifyRegex();

    // Redirection patterns
    [GeneratedRegex(@"(?<!\d)(>{1,2})\s*([^\s|;&><]+)", RegexOptions.Compiled)]
    private static partial Regex OutputRedirectRegex();

    [GeneratedRegex(@"<\s*([^\s|;&><]+)", RegexOptions.Compiled)]
    private static partial Regex InputRedirectRegex();

    public BashAnalysisResult Analyze(string command)
    {
        var result = new BashAnalysisResult();

        if (string.IsNullOrWhiteSpace(command))
            return result;

        // First check for dangerous patterns
        var (isDangerous, reason) = CheckDangerousPatterns(command);
        if (isDangerous)
        {
            result.HasDangerousPattern = true;
            result.DangerousPatternReason = reason;
            return result;
        }

        // Check for bypass attempts
        CheckBypassAttempts(command, result);

        // Split command by separators (;, &&, ||, newlines)
        var subCommands = SplitCommand(command);

        foreach (var subCmd in subCommands)
        {
            AnalyzeSubCommand(subCmd.Trim(), result);
        }

        return result;
    }

    public (bool IsDangerous, string? Reason) CheckDangerousPatterns(string command)
    {
        foreach (var (pattern, reason) in DangerousPatterns)
        {
            if (pattern.IsMatch(command))
                return (true, reason);
        }
        return (false, null);
    }

    private static void CheckBypassAttempts(string command, BashAnalysisResult result)
    {
        // Check for command substitution
        if (CommandSubstitutionRegex().IsMatch(command))
        {
            result.Warnings.Add("Command contains command substitution - paths may be dynamic");
        }

        // Check for base64/hex encoding (potential obfuscation)
        if (Base64DecodeRegex().IsMatch(command))
        {
            result.Warnings.Add("Command contains base64 decode - potential obfuscation");
        }

        if (HexDecodeRegex().IsMatch(command))
        {
            result.Warnings.Add("Command contains hex decode - potential obfuscation");
        }

        // Check for variable expansion
        if (VariableExpansionRegex().IsMatch(command))
        {
            result.Warnings.Add("Command contains variable expansion - paths may be dynamic");
        }

        // Check for newline injection
        if (command.Contains('\n') || command.Contains("$'\\n'") || command.Contains("%0a"))
        {
            result.Warnings.Add("Command contains embedded newlines");
        }
    }

    private static IEnumerable<string> SplitCommand(string command)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escapeNext = false;

        for (int i = 0; i < command.Length; i++)
        {
            var c = command[i];

            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                current.Append(c);
                continue;
            }

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                current.Append(c);
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                current.Append(c);
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote)
            {
                if (c == ';' || c == '\n')
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }

                if (c == '&' && i + 1 < command.Length && command[i + 1] == '&')
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    i++; // Skip second &
                    continue;
                }

                if (c == '|' && i + 1 < command.Length && command[i + 1] == '|')
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    i++; // Skip second |
                    continue;
                }

                // Single & (background) - still part of this command but note it
                // Single | (pipe) - keep as part of command, will analyze for redirections
            }

            current.Append(c);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    private void AnalyzeSubCommand(string command, BashAnalysisResult result)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        // Extract the command name
        var tokens = TokenizeCommand(command);
        if (tokens.Count == 0)
            return;

        var cmdName = tokens[0];

        // Check for sed -i (in-place edit) - special case
        if (cmdName.Equals("sed", StringComparison.OrdinalIgnoreCase))
        {
            if (tokens.Any(t => t == "-i" || t.StartsWith("-i") || t == "--in-place"))
            {
                // Check for files at the end (after the pattern)
                var lastToken = tokens.LastOrDefault();
                if (lastToken != null && !lastToken.StartsWith("-") && LooksLikePath(lastToken))
                {
                    result.Operations.Add(new FileOperation
                    {
                        Type = FileOperationType.Write,
                        Path = lastToken,
                        Command = "sed -i"
                    });
                }
            }
            else
            {
                // Regular sed without -i is a read operation
                var files = ExtractPaths(tokens.Skip(1));
                foreach (var file in files)
                {
                    result.Operations.Add(new FileOperation
                    {
                        Type = FileOperationType.Read,
                        Path = file,
                        Command = "sed"
                    });
                }
            }
        }

        // Check for awk with redirection
        if (cmdName.Equals("awk", StringComparison.OrdinalIgnoreCase) ||
            cmdName.Equals("gawk", StringComparison.OrdinalIgnoreCase))
        {
            // awk itself reads files
            var inputFiles = ExtractPaths(tokens.Skip(1).Where(t => !t.StartsWith("'") && !t.StartsWith("\"")));
            foreach (var file in inputFiles)
            {
                result.Operations.Add(new FileOperation
                {
                    Type = FileOperationType.Read,
                    Path = file,
                    Command = cmdName
                });
            }
        }

        // Check redirection operators (applies to all commands)
        AnalyzeRedirection(command, result);

        // Check known commands
        if (ReadCommands.TryGetValue(cmdName, out var readType))
        {
            var paths = ExtractPaths(tokens.Skip(1));
            foreach (var path in paths)
            {
                result.Operations.Add(new FileOperation
                {
                    Type = readType,
                    Path = path,
                    Command = cmdName
                });
            }
        }

        if (WriteCommands.TryGetValue(cmdName, out var writeType))
        {
            var paths = ExtractPaths(tokens.Skip(1));
            foreach (var path in paths)
            {
                result.Operations.Add(new FileOperation
                {
                    Type = writeType,
                    Path = path,
                    Command = cmdName
                });
            }
        }

        if (DeleteCommands.TryGetValue(cmdName, out var deleteType))
        {
            var paths = ExtractPaths(tokens.Skip(1));
            foreach (var path in paths)
            {
                result.Operations.Add(new FileOperation
                {
                    Type = deleteType,
                    Path = path,
                    Command = cmdName
                });
            }
        }

        if (PermissionCommands.TryGetValue(cmdName, out var permType))
        {
            var paths = ExtractPaths(tokens.Skip(1));
            foreach (var path in paths)
            {
                result.Operations.Add(new FileOperation
                {
                    Type = permType,
                    Path = path,
                    Command = cmdName
                });
            }
        }

        if (CopyMoveCommands.TryGetValue(cmdName, out var copyMoveType))
        {
            var paths = ExtractPaths(tokens.Skip(1));
            foreach (var path in paths)
            {
                result.Operations.Add(new FileOperation
                {
                    Type = copyMoveType,
                    Path = path,
                    Command = cmdName
                });
            }
        }
    }

    private static void AnalyzeRedirection(string command, BashAnalysisResult result)
    {
        // Output redirection: > or >>
        var outputRedirects = OutputRedirectRegex().Matches(command);
        foreach (Match match in outputRedirects)
        {
            var path = match.Groups[2].Value.Trim('"', '\'');
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("&") && LooksLikePath(path))
            {
                var redirectOp = match.Groups[1].Value;
                result.Operations.Add(new FileOperation
                {
                    Type = FileOperationType.Write,
                    Path = path,
                    Command = redirectOp
                });
            }
        }

        // Input redirection: <
        var inputRedirects = InputRedirectRegex().Matches(command);
        foreach (Match match in inputRedirects)
        {
            var path = match.Groups[1].Value.Trim('"', '\'');
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("<") && LooksLikePath(path))
            {
                result.Operations.Add(new FileOperation
                {
                    Type = FileOperationType.Read,
                    Path = path,
                    Command = "<"
                });
            }
        }
    }

    private static List<string> TokenizeCommand(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escapeNext = false;

        foreach (var c in command)
        {
            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\' && !inSingleQuote)
            {
                escapeNext = true;
                continue;
            }

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inSingleQuote && !inDoubleQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static IEnumerable<string> ExtractPaths(IEnumerable<string> tokens)
    {
        return tokens
            .Where(t => !t.StartsWith("-") && !string.IsNullOrEmpty(t))
            .Where(t => !IsShellOperator(t))
            .Where(LooksLikePath);
    }

    private static bool IsShellOperator(string token)
    {
        return token is "|" or ">" or ">>" or "<" or "<<" or "&&" or "||" or ";" or "&" or "2>" or "2>>" or "&>" or "|&";
    }

    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Skip flags and shell operators
        if (value.StartsWith('-') || value.StartsWith("&") || value.StartsWith("2>"))
            return false;

        // Skip numbers (file descriptors)
        if (int.TryParse(value, out _))
            return false;

        // Skip PowerShell parameters
        if (value.StartsWith("-") && value.Contains(":"))
            return false;

        // Has path separators or file extension - likely a path
        if (value.Contains('/') || value.Contains('\\') || (value.Contains('.') && !value.StartsWith(".")))
            return true;

        // Known extensions
        var extensions = new[] { ".txt", ".json", ".yaml", ".yml", ".xml", ".md", ".env", ".sh", ".ps1", ".py", ".cs", ".js", ".ts" };
        if (extensions.Any(ext => value.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Common sensitive filenames
        var sensitiveNames = new[] { ".env", ".npmrc", ".pypirc", "secrets", "credentials", "config", "passwd", "shadow" };
        if (sensitiveNames.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Single word without special chars could be a filename
        if (!value.Contains(' ') && value.Length < 100)
            return true;

        return false;
    }
}
