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

        // Worktree lifecycle — must go through dydo
        (GitWorktreeAddRegex(), "Use dydo dispatch --worktree to create worktrees"),
        (GitWorktreeRemoveRegex(), "Use dydo worktree cleanup to remove worktrees"),
    ];

    // Heredoc detection — strip $(cat <<'WORD'...WORD) to prevent false positives
    [GeneratedRegex(@"\$\(cat\s+<<-?'?""?(\w+)""?'?\s*\n[\s\S]*?\n\1\s*\)")]
    private static partial Regex CatHeredocRegex();

    // Bypass detection patterns
    [GeneratedRegex(@"\$\([^)]+\)|`[^`]+`")]
    private static partial Regex CommandSubstitutionRegex();

    [GeneratedRegex(@"base64\s+(-d|--decode)", RegexOptions.IgnoreCase)]
    private static partial Regex Base64DecodeRegex();

    [GeneratedRegex(@"xxd\s+-r|od\s+-A")]
    private static partial Regex HexDecodeRegex();

    [GeneratedRegex(@"\$[A-Za-z_][A-Za-z0-9_]*|\$\{[^}]+\}")]
    private static partial Regex VariableExpansionRegex();

    // Dangerous pattern regexes (generated for performance)
    [GeneratedRegex(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+(/|~|/\*)(\s+--[a-z-]+)*\s*($|;|&&|\|\||&|\|)")]
    private static partial Regex RecursiveDeleteRootRegex();

    [GeneratedRegex(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+\*\s*($|;|&&|\|\||&|\|)")]
    private static partial Regex RecursiveDeleteGlobRegex();

    [GeneratedRegex(@":\s*\(\s*\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:")]
    private static partial Regex ForkBombClassicRegex();

    [GeneratedRegex(@"\.\s*/\s*\.:")]
    private static partial Regex ForkBombAltRegex();

    [GeneratedRegex(@">\s*/dev/sd[a-z]")]
    private static partial Regex DirectDiskWriteRegex();

    [GeneratedRegex(@"dd\s+.*of\s*=\s*/dev/sd[a-z]", RegexOptions.IgnoreCase)]
    private static partial Regex DdDiskWriteRegex();

    [GeneratedRegex(@"curl\s+[^|]*\|\s*(ba)?sh", RegexOptions.IgnoreCase)]
    private static partial Regex CurlPipeShRegex();

    [GeneratedRegex(@"wget\s+[^|]*\|\s*(ba)?sh", RegexOptions.IgnoreCase)]
    private static partial Regex WgetPipeShRegex();

    [GeneratedRegex(@"wget\s+-O\s*-?\s+[^|]*\|\s*(ba)?sh", RegexOptions.IgnoreCase)]
    private static partial Regex WgetOutputPipeShRegex();

    [GeneratedRegex(@"Invoke-WebRequest[^|]*\|\s*Invoke-Expression", RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellIwrIexRegex();

    [GeneratedRegex(@"iwr\s+[^|]*\|\s*iex", RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellIwrIexShortRegex();

    [GeneratedRegex(@"DownloadString\s*\([^)]+\)[^|]*\|\s*(iex|Invoke-Expression)", RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellDownloadStringRegex();

    [GeneratedRegex(@"eval\s+\$")]
    private static partial Regex EvalVariableRegex();

    [GeneratedRegex(@"history\s+-c")]
    private static partial Regex HistoryClearRegex();

    [GeneratedRegex(@">\s*~/\.bash_history|>\s*~/\.zsh_history")]
    private static partial Regex HistoryFileTruncateRegex();

    [GeneratedRegex(@"Remove-Item.*ConsoleHost_history\.txt", RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellHistoryDeleteRegex();

    [GeneratedRegex(@"setenforce\s+0")]
    private static partial Regex SelinuxDisableRegex();

    [GeneratedRegex(@"iptables\s+-F")]
    private static partial Regex FirewallFlushRegex();

    [GeneratedRegex(@"cat\s+/etc/shadow|head\s+/etc/shadow|tail\s+/etc/shadow")]
    private static partial Regex ShadowFileAccessRegex();

    [GeneratedRegex(@">\s*/etc/passwd|echo.*>>\s*/etc/passwd")]
    private static partial Regex PasswdModifyRegex();

    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+worktree\s+add(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitWorktreeAddRegex();

    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+worktree\s+remove(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitWorktreeRemoveRegex();

    // Coaching: detect needless cd+command compounds
    [GeneratedRegex(@"^\s*cd\s+(?:""([^""]+)""|'([^']+)'|(\S+))\s*(?:&&|;)\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex CdThenCommandRegex();

    // Redirection patterns
    [GeneratedRegex(@"(?<!\d)(>{1,2})\s*([^\s|;&><]+)")]
    private static partial Regex OutputRedirectRegex();

    [GeneratedRegex(@"<\s*([^\s|;&><]+)")]
    private static partial Regex InputRedirectRegex();

    public (bool IsMatch, string? CdPath, string? RestCommand) DetectNeedlessCd(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return (false, null, null);

        var match = CdThenCommandRegex().Match(command);
        if (!match.Success)
            return (false, null, null);

        var cdPath = match.Groups[1].Success ? match.Groups[1].Value
                   : match.Groups[2].Success ? match.Groups[2].Value
                   : match.Groups[3].Value;

        return (true, cdPath, match.Groups[4].Value.Trim());
    }

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

        // Strip cat heredoc blocks — literal text, not shell code
        var strippedCommand = CatHeredocRegex().Replace(command, "HEREDOC_STRIPPED");

        // Check for bypass attempts
        CheckBypassAttempts(strippedCommand, result);

        // Split command by separators (;, &&, ||, newlines)
        var subCommands = SplitCommand(strippedCommand);

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

    private static readonly (Func<string, bool> Check, string Warning)[] BypassChecks =
    [
        (cmd => CommandSubstitutionRegex().IsMatch(cmd), "Command contains command substitution - paths may be dynamic"),
        (cmd => Base64DecodeRegex().IsMatch(cmd), "Command contains base64 decode - potential obfuscation"),
        (cmd => HexDecodeRegex().IsMatch(cmd), "Command contains hex decode - potential obfuscation"),
        (cmd => VariableExpansionRegex().IsMatch(cmd), "Command contains variable expansion - paths may be dynamic"),
        (cmd => cmd.Contains('\n') || cmd.Contains("$'\\n'") || cmd.Contains("%0a"), "Command contains embedded newlines"),
    ];

    private static void CheckBypassAttempts(string command, BashAnalysisResult result)
    {
        foreach (var (check, warning) in BypassChecks)
        {
            if (check(command))
                result.Warnings.Add(warning);
        }
    }

    private static int CheckCommandSeparator(string command, int i)
    {
        var c = command[i];
        if (c == ';' || c == '\n')
            return 1;
        if (c == '&' && i + 1 < command.Length && command[i + 1] == '&')
            return 2;
        if (c == '|' && i + 1 < command.Length && command[i + 1] == '|')
            return 2;
        return 0;
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
                var sepLen = CheckCommandSeparator(command, i);
                if (sepLen > 0)
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    i += sepLen - 1;
                    continue;
                }
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

        // Check all known command dictionaries with single unified lookup
        if (AllCommands.TryGetValue(cmdName, out var opType))
        {
            AddPathOperations(result, tokens.Skip(1), opType, cmdName);
        }
    }

    private static readonly Dictionary<string, FileOperationType> AllCommands = BuildAllCommands();

    private static Dictionary<string, FileOperationType> BuildAllCommands()
    {
        var all = new Dictionary<string, FileOperationType>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in ReadCommands) all.TryAdd(k, v);
        foreach (var (k, v) in WriteCommands) all.TryAdd(k, v);
        foreach (var (k, v) in DeleteCommands) all.TryAdd(k, v);
        foreach (var (k, v) in PermissionCommands) all.TryAdd(k, v);
        foreach (var (k, v) in CopyMoveCommands) all.TryAdd(k, v);
        return all;
    }

    private static void AddPathOperations(BashAnalysisResult result, IEnumerable<string> tokens, FileOperationType type, string cmdName)
    {
        foreach (var path in ExtractPaths(tokens))
        {
            result.Operations.Add(new FileOperation
            {
                Type = type,
                Path = path,
                Command = cmdName
            });
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

    private static readonly HashSet<string> ShellOperators =
        ["|", ">", ">>", "<", "<<", "&&", "||", ";", "&", "2>", "2>>", "&>", "|&"];

    private static bool IsShellOperator(string token) => ShellOperators.Contains(token);

    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".json", ".yaml", ".yml", ".xml", ".md", ".env", ".sh", ".ps1", ".py", ".cs", ".js", ".ts" };

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
        { ".env", ".npmrc", ".pypirc", "secrets", "credentials", "config", "passwd", "shadow" };

    private static bool IsDigitColonPattern(string value)
    {
        foreach (var c in value)
        {
            if (c != ':' && !char.IsDigit(c))
                return false;
        }
        return value.Contains(':');
    }

    private static bool HasKnownExtension(string value)
    {
        var dot = value.LastIndexOf('.');
        return dot >= 0 && KnownExtensions.Contains(value[dot..]);
    }

    private static bool HasSensitiveName(string value)
    {
        foreach (var name in SensitiveNames)
        {
            if (value.Contains(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsNotPathPrefix(string value)
    {
        if (value.StartsWith('-') || value.StartsWith("&") || value.StartsWith("2>"))
            return true;
        if (int.TryParse(value, out _))
            return true;
        if (value.StartsWith("-") && value.Contains(":"))
            return true;
        return false;
    }

    private static bool HasPathIndicators(string value)
    {
        return value.Contains('/') || value.Contains('\\') || (value.Contains('.') && !value.StartsWith("."));
    }

    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (IsNotPathPrefix(value))
            return false;

        if (HasPathIndicators(value) || HasKnownExtension(value) || HasSensitiveName(value))
            return true;

        if (value.AsSpan().IndexOfAny("[]{}") >= 0 || IsDigitColonPattern(value))
            return false;

        return !value.Contains(' ') && value.Length < 100;
    }
}
