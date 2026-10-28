namespace DynaDocs.Services;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Detects file operations in bash, zsh, PowerShell and cmd commands.
/// </summary>
public partial class BashCommandAnalyzer : IBashCommandAnalyzer
{
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

    // Upstream's anchor for the command-shaped denylist entries: a command name starts at the beginning
    // or after whitespace or a shell separator, so wrappers, launchers, assignments and keywords need no
    // list. A quote opens a command only after a shell -c flag or an ssh remote command. An optional
    // path prefix follows (/usr/bin/dd). Names inside quoted prose after whitespace match too: fail closed.
    private const string CommandContext = @"^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""]";
    private const string CommandPath = @"(?:[^\s;&|'""`()]*/)?";
    private const string CommandStart = "(?<=" + CommandContext + ")" + CommandPath;

    // NonBacktracking has no lookarounds, so its patterns consume the context instead; for IsMatch that
    // is equivalent.
    private const string LinearCommandStart = "(?:" + CommandContext + ")" + CommandPath;

    private const int DangerousPatternTimeoutMs = 250;

    // A command word ends at whitespace, the end, a quote or backtick closing a -c string or substitution, or a separator.
    private const string CommandEndContext = @"\s|$|['""`);&|]";
    private const string CommandEnd = "(?=" + CommandEndContext + ")";

    private static readonly (Func<string, bool> IsMatch, string Reason)[] DangerousPatterns =
    [
        (RecursiveDeleteRootRegex().IsMatch, "Recursive delete of root or home directory"),
        (RecursiveDeleteGlobRegex().IsMatch, "Recursive delete with dangerous glob pattern"),

        (PowerShellRecursiveDeleteRegex().IsMatch, "PowerShell recursive delete of root directory"),

        (ForkBombClassicRegex().IsMatch, "Fork bomb detected"),
        (ForkBombAltRegex().IsMatch, "Fork bomb variant detected"),

        (DirectDiskWriteRegex().IsMatch, "Direct disk write attempt"),
        (DdDiskWriteRegex().IsMatch, "Direct disk write via dd"),
        (DdKnownDiskWriteRegex().IsMatch, "Direct disk write via dd"),

        (Base64DecodePipeExecRegex().IsMatch, "Base64 decode piped to interpreter"),

        (CurlPipeShRegex().IsMatch, "Download and execute pattern (curl | sh)"),
        (WgetPipeShRegex().IsMatch, "Download and execute pattern (wget | sh)"),
        (WgetOutputPipeShRegex().IsMatch, "Download and execute pattern (wget -O | sh)"),

        (PowerShellIwrIexRegex().IsMatch, "Download and execute pattern (PowerShell IWR | IEX)"),
        (PowerShellIwrIexShortRegex().IsMatch, "Download and execute pattern (PowerShell iwr | iex)"),
        (PowerShellDownloadStringRegex().IsMatch, "Download and execute pattern (DownloadString | IEX)"),

        (EvalVariableRegex().IsMatch, "Eval of variable content"),

        (HistoryClearRegex().IsMatch, "History clearing detected"),
        (HistoryFileTruncateRegex().IsMatch, "History file truncation"),
        (PowerShellHistoryDeleteRegex().IsMatch, "PowerShell history deletion"),

        (SelinuxDisableRegex().IsMatch, "SELinux disable attempt"),
        (FirewallFlushRegex().IsMatch, "Firewall flush attempt"),

        (ShadowFileAccessRegex().IsMatch, "Shadow file access attempt"),
        (PasswdModifyRegex().IsMatch, "Password file modification attempt"),

        // David Ondrej's global command denylist, pinned in DYD-188.
        (OndrejRmRegex().IsMatch, "Deletion of system or home directory"),
        (OndrejDiskRegex().IsMatch, "Disk format or erase attempt"),
        (OndrejSudoRmRegex().IsMatch, "Privileged delete attempt"),
        (OndrejCurlPipeRegex().IsMatch, "Download and execute as shell"),
        (OndrejGitPushRegex().IsMatch, "Force push or remote branch deletion. GitHub deletes a merged PR's branch itself; clean up locally from outside the tree with dydo clean --dry-run <worktree>, then dydo clean <worktree>, which removes the worktree, keeps unmerged branches and runs git fetch --prune; name any other remote branch for the user"),
        (OndrejGitRecoveryRegex().IsMatch, "Git recovery history destruction"),
        (OndrejSystemPermissionsRegex().IsMatch, "System permission or ownership change"),
        (OndrejGhDeleteRegex().IsMatch, "Destructive GitHub operation"),
        (OndrejGhApiDeleteRegex().IsMatch, "GitHub API delete operation"),
        (OndrejGhVisibilityRegex().IsMatch, "GitHub repository visibility change"),
        (OndrejGhTokenRegex().IsMatch, "GitHub authentication token access"),
        (OndrejPasswordCliRegex().IsMatch, "Password manager access"),
        (RunsPasswordStore, "Password store access"),
        (OndrejOpRegex().IsMatch, "1Password secret access"),
        (OndrejKeychainRegex().IsMatch, "Keychain password access"),
        (OndrejGpgSecretRegex().IsMatch, "GPG secret key export"),
        (OndrejPasswordStorePathRegex().IsMatch, "Password store path access"),
        (OndrejPasswordAppRegex().IsMatch, "Password manager application access"),

        // Raw worktree removal - dydo clean archives uncommitted work and never follows a link.
        (ForcedWorktreeRemoveRegex().IsMatch, WorktreeRemovalReason),
        (RecursiveWorktreeDeleteRegex().IsMatch, WorktreeRemovalReason),

        (InlineInterpreterRegex().IsMatch, "Inline interpreter execution bypasses file operation analysis. Write a script file instead."),
        (RunsPythonFromStdin, "Python program on stdin bypasses file operation analysis, as python -c does. "
            + "Change files with the file tools or write a script file; a change to a protected file goes to the user."),
    ];

    // Heredoc detection - strip $(cat <<'WORD'...WORD) to prevent false positives
    [GeneratedRegex(@"\$\(cat\s+<<-?'?""?(\w+)""?'?\s*\n[\s\S]*?\n\1\s*\)")]
    private static partial Regex CatHeredocRegex();

    // Any heredoc (<<WORD, <<'WORD', <<"WORD", <<-WORD): group 1 is the opener line, then the body and
    // the delimiter line with its line break. Group 3 is the delimiter's quote: a quoted delimiter
    // makes the body literal. With <<- the delimiter may be tab-indented.
    [GeneratedRegex(@"(?<!<)(<<(-)?[ \t]*(['""]?)(\w+)\3[^\n]*\n)[\s\S]*?^(?(2)\t*)\4[ \t]*\r?$\n?", RegexOptions.Multiline)]
    private static partial Regex HeredocBodyRegex();

    // A PowerShell here-string: @' or @" ends its line, '@ or "@ starts the closing line. @' is literal.
    [GeneratedRegex(@"@(['""])[ \t]*\r?\n[\s\S]*?^\1@", RegexOptions.Multiline)]
    private static partial Regex HereStringRegex();

    // A shell or interpreter named around a heredoc or here-string runs its body (bash <<'EOF',
    // @'...'@ | python -), so that body is code, not data.
    [GeneratedRegex(@"(?:^|[\s;&|(])(?:[^\s;&|()'""]*[/\\])?(?:bash|sh|zsh|dash|ksh|pwsh|powershell|cmd|python[0-9.]*|py|node|ruby|perl|php|eval|source|iex|invoke-expression)(?:\.exe)?(?=$|[\s;&|)])", RegexOptions.IgnoreCase)]
    private static partial Regex BodyRunnerRegex();

    /// <summary>
    /// Removes heredoc and PowerShell here-string bodies, which are text a command reads, so a line
    /// inside one does not split out as a command and its newlines and $ signs are data. A heredoc
    /// leaves its opener line and a separator, a here-string an empty string, so commands after the
    /// closing line remain. A body a shell or interpreter around it runs stays. With
    /// <paramref name="keepExpanding"/>, the bodies a shell expands (&lt;&lt;EOF, @"...") stay too,
    /// so their substitutions and variables are still seen.
    /// </summary>
    internal static string StripLiteralBodies(string command, bool keepExpanding = false)
    {
        var heredocsOut = HeredocBodyRegex().Replace(command, body =>
            (keepExpanding && body.Groups[3].Length == 0) || IsRunBody(command, OpenerLineEnd(body), OpenerLineEnd(body))
                ? body.Value
                : body.Groups[1].Value.TrimEnd() + "; ");
        return HereStringRegex().Replace(heredocsOut, body =>
            (keepExpanding && body.Groups[1].Value == "\"") || IsRunBody(heredocsOut, body.Index, body.Index + body.Length)
                ? body.Value
                : "''");
    }

    // A heredoc's opener line ends where its body begins, before that line's break.
    private static int OpenerLineEnd(Match heredoc) => heredoc.Index + heredoc.Groups[1].Value.TrimEnd().Length;

    // Whether a runner is named on the line before start or after end: a heredoc's opener line, or the
    // lines a here-string opens and closes on, the body's own lines left out.
    private static bool IsRunBody(string text, int start, int end)
    {
        var lineEnd = text.IndexOf('\n', end);
        return BodyRunnerRegex().IsMatch(text[LineStart(text, start)..start] + " " + text[end..(lineEnd < 0 ? text.Length : lineEnd)]);
    }

    private static int LineStart(string text, int index) => index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;

    // A double-quoted string (backslash escapes skipped, lines spanned) or a single-quoted one, the
    // first that starts; $'...' is bash's escape-reading form.
    [GeneratedRegex(@"""(?:[^""\\]|\\.)*""|(?<ansi>\$)?'(?<single>[^']*)'")]
    private static partial Regex QuotedTextRegex();

    // The text before a quote that makes it a command string a shell or eval runs: -c, -Command and its
    // prefixes, cmd's /c or /k, env -S, eval, iex, trap, watch, or an ssh remote command.
    [GeneratedRegex(@"(?:^|\s)(?:-[A-Za-z]*c|-co(?:m(?:m(?:a(?:n(?:d)?)?)?)?)?|/[ck]|-S|--split-string=?|eval|iex|invoke-expression|trap|watch|ssh(?:\s+\S+)+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CommandStringContextRegex();

    /// <summary>
    /// What a shell expands: the text without literal bodies and with each literal single-quoted
    /// string emptied.
    /// </summary>
    private static string ExpandingText(string command)
    {
        var text = StripLiteralBodies(command, keepExpanding: true);
        var words = new Lazy<List<string>>(() => [.. new[] { true, false }
            .SelectMany(posix => ShellReader.Read(text, posix).Commands).SelectMany(read => read.Words)]);
        return QuotedTextRegex().Replace(text, quoted => IsLiteralSingleQuoted(text, quoted, words) ? "''" : quoted.Value);
    }

    // Single-quoted text taken literally: not bash's $'...' and not a command string something runs
    // (bash -c '...'). Text spanning lines must also be in a word as the line reads, so an apostrophe
    // in a comment or an expanding heredoc body, which quotes nothing, cannot hide the lines after it.
    private static bool IsLiteralSingleQuoted(string text, Match quoted, Lazy<List<string>> words)
    {
        var single = quoted.Groups["single"];
        return single.Success && !quoted.Groups["ansi"].Success
            && !CommandStringContextRegex().IsMatch(text[LineStart(text, quoted.Index)..quoted.Index])
            && (!single.Value.Contains('\n') || words.Value.Any(word => word.Contains(single.Value, StringComparison.Ordinal)));
    }

    // The command's layout: bodies gone and every quoted string emptied, so a line break left in it
    // separates commands.
    private static string Layout(string analysed) =>
        QuotedTextRegex().Replace(analysed, quoted => quoted.Groups["single"].Success ? "''" : "\"\"");

    // Bypass detection patterns
    [GeneratedRegex(@"\$\([^)]+\)|`[^`]+`")]
    private static partial Regex CommandSubstitutionRegex();

    [GeneratedRegex(@"base64\s+(-d|--decode)", RegexOptions.IgnoreCase)]
    private static partial Regex Base64DecodeRegex();

    [GeneratedRegex(@"xxd\s+-r|od\s+-A")]
    private static partial Regex HexDecodeRegex();

    [GeneratedRegex(@"\$[A-Za-z_][A-Za-z0-9_]*|\$\{[^}]+\}")]
    private static partial Regex VariableExpansionRegex();

    [GeneratedRegex(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+(\./|/|~|/\*)(\s+--[a-z-]+)*\s*($|;|&&|\|\||&|\|)", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex RecursiveDeleteRootRegex();

    [GeneratedRegex(@"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+\*\s*($|;|&&|\|\||&|\|)", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex RecursiveDeleteGlobRegex();

    [GeneratedRegex(@"Remove-Item\b(?=.*-Recurse\b)(?=.*-Force\b).*(\s[/~]|\s[A-Z]:\\)\s*($|;|&&|\|\|)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex PowerShellRecursiveDeleteRegex();

    [GeneratedRegex(@":\s*\(\s*\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex ForkBombClassicRegex();

    [GeneratedRegex(@"\.\s*/\s*\.:", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex ForkBombAltRegex();

    [GeneratedRegex(@">\s*/dev/(?:r?disk\d*|sd[a-z]|nvme\d|vd[a-z]|mmcblk\d)", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex DirectDiskWriteRegex();

    [GeneratedRegex(CommandStart + @"dd\s+[^;&|\r\n]*\bof\s*=\s*['""]?/dev/(?!null(?:\s|$|['"";&|)`]))[^\s'"";&|]+", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex DdDiskWriteRegex();

    // Kept unanchored on purpose: known disk targets block anywhere in the command text.
    [GeneratedRegex(@"dd\s+.*of\s*=\s*/dev/(?:sd[a-z]|nvme\d|vd[a-z]|mmcblk\d)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex DdKnownDiskWriteRegex();

    [GeneratedRegex(@"base64\s+(-d|--decode)[^|]*\|\s*(python[23]?|bash|sh|zsh|perl|ruby|node|pwsh|powershell)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex Base64DecodePipeExecRegex();

    [GeneratedRegex(@"curl\s+[^|]*\|\s*(ba)?sh", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex CurlPipeShRegex();

    [GeneratedRegex(@"wget\s+[^|]*\|\s*(ba)?sh", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex WgetPipeShRegex();

    [GeneratedRegex(@"wget\s+-O\s*-?\s+[^|]*\|\s*(ba)?sh", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex WgetOutputPipeShRegex();

    [GeneratedRegex(@"Invoke-WebRequest[^|]*\|\s*Invoke-Expression", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex PowerShellIwrIexRegex();

    [GeneratedRegex(@"iwr\s+[^|]*\|\s*iex", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex PowerShellIwrIexShortRegex();

    [GeneratedRegex(@"DownloadString\s*\([^)]+\)[^|]*\|\s*(iex|Invoke-Expression)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex PowerShellDownloadStringRegex();

    [GeneratedRegex(@"eval\s+\$", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex EvalVariableRegex();

    [GeneratedRegex(@"history\s+-c", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex HistoryClearRegex();

    [GeneratedRegex(@">\s*~/\.bash_history|>\s*~/\.zsh_history", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex HistoryFileTruncateRegex();

    [GeneratedRegex(@"Remove-Item.*ConsoleHost_history\.txt", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex PowerShellHistoryDeleteRegex();

    [GeneratedRegex(@"setenforce\s+0", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex SelinuxDisableRegex();

    [GeneratedRegex(@"iptables\s+-F", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex FirewallFlushRegex();

    [GeneratedRegex(@"cat\s+/etc/shadow|head\s+/etc/shadow|tail\s+/etc/shadow", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex ShadowFileAccessRegex();

    [GeneratedRegex(@">\s*/etc/passwd|echo.*>>\s*/etc/passwd", RegexOptions.None, DangerousPatternTimeoutMs)]
    private static partial Regex PasswdModifyRegex();

    [GeneratedRegex(CommandStart + @"rm\s+(?:(?:-[a-zA-Z]+|--[a-z-]+)\s+)*(?:['"" ]?/(?:Users(?:/[^/\s'"";&|]+)?/?|\*|)|['"" ]?(?:~|\$HOME|\$\{HOME\})(?:/\*)?/?|[^;&|]*--no-preserve-root)(?:['""\s]|$|[;&|])", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejRmRegex();

    [GeneratedRegex(CommandStart + @"(?:mkfs(?:\.[a-z0-9]+)?" + CommandEnd + @"|diskutil\s+(?:erase\w*|partitionDisk|zeroDisk|secureErase|apfs\s+(?:delete|erase)\w*)" + CommandEnd + @")|>\s*/dev/(?:r?disk\d*|sd[a-z]|nvme\d+)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejDiskRegex();

    // Every sudo inside a chain of option arguments is also a command start, and each backtracking restart
    // rescans the rest of the chain: quadratic in its length. NonBacktracking matches in linear time.
    [GeneratedRegex(LinearCommandStart + @"sudo(?:\s+-[a-zA-Z-]*(?:=[^\s;&|]*|\s+[^-\s;&|][^\s;&|]*)?)*\s+rm(?:" + CommandEndContext + ")", RegexOptions.NonBacktracking, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejSudoRmRegex();

    [GeneratedRegex(CommandStart + @"(?:curl|wget)\s+[^;&|]*\|\s*(?:sudo\s+)?(?:ba|z|da)?sh" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejCurlPipeRegex();

    [GeneratedRegex(CommandStart + @"git\s+push" + CommandEnd + @"[^;&|\r\n]*(?:\s(?:-f|--force|--delete|-d)" + CommandEnd + @"|\s\+[a-z0-9._/-]|\s:[a-z0-9._/-])", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGitPushRegex();

    [GeneratedRegex(CommandStart + @"git\s+(?:reflog\s+expire\b[^;&|\r\n]*--expire(?:-unreachable)?(?:=|\s+)now\b|gc\b[^;&|\r\n]*--prune(?:=|\s+)(?:now|all)\b)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGitRecoveryRegex();

    [GeneratedRegex(CommandStart + @"(?:chmod\s+[^;&|\r\n]*\b777\s['""]?/['""]?(?=\s|$|[;&|])|chown\s+-R\b[^;&|\r\n]*\s['""]?/['""]?(?=\s|$|[;&|]))", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejSystemPermissionsRegex();

    [GeneratedRegex(CommandStart + @"gh\s+(?:(?:repo|release|secret|ssh-key|gpg-key)\s+delete)" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGhDeleteRegex();

    [GeneratedRegex(CommandStart + @"gh\s+api\b[^;&|\r\n]*(?:-X|--method)(?:=|\s+)DELETE" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGhApiDeleteRegex();

    [GeneratedRegex(CommandStart + @"gh\s+repo\s+edit\b[^;&|\r\n]*--visibility(?:=|\s+)public" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGhVisibilityRegex();

    [GeneratedRegex(CommandStart + @"gh\s+auth\s+token" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGhTokenRegex();

    [GeneratedRegex(CommandStart + @"(?:bw|bws|lpass|keepassxc-cli|rbw|nordpass)" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejPasswordCliRegex();

    // Deliberate difference from upstream DYD-188: the original `pass\s+\S` also matches prose
    // shaped like the password-store CLI (a body line, a table cell, "pass the lease to..."),
    // so a PR body or commit message containing the word "pass" gets denied. This rule blocks
    // three shapes: a known subcommand, a flag, or exactly one name token (`pass <name>` prints
    // the secret) that is bare, quoted (`pass "My Bank"`, spaces allowed inside the quotes) or a
    // `$(...)` substitution. Prose with two or more words after "pass" stays allowed.
    // Named gaps, accepted on purpose: an unquoted all-digit name (`pass 12`), a second word
    // (`pass x y`), an fd of more than one digit (`pass x 10>f`), and the name form written as a
    // markdown table cell: `pass` after a single pipe with its name before a single pipe, as in
    // "| unit | pass OK |", which also lets `true | pass github | xclip` through (the name form
    // ignores stdin). Subcommand and flag forms stay blocked inside a table cell.
    // The subcommand must end on a real word boundary, not `\b`, which also fires between a
    // letter and a hyphen, so "pass show-stopper" would otherwise match "show". After the name,
    // optional spaces lead to one terminator: end, newline, a separator or quote, a redirect with
    // at most one fd digit tied to it (`pass x > f`, `pass x 2>&1`, `pass x &> f`), or a `#`
    // comment. A digit not followed by a redirect is prose ("pass stage 2"). gopass takes the same
    // shapes. The lead-in skips only spaces and tabs, the path prefix stops
    // at `{`, and the quoted and `$(...)` names stop at a newline, quote or paren: each newline
    // or `{` is its own start, so a long run of them cannot rescan the input from every start.
    // These shapes are read on the raw text; RunsPasswordStore then asks that a pass or gopass
    // runs at command position, so a heredoc body, a quoted grep pattern or a PR body saying
    // "pass" is no password-store access (DYD-406).
    [GeneratedRegex(@"(?:^|(?<cell>(?<!\|)\|(?!\|))|\|\||[;&\r\n({`])[ \t]*(?:[^\s;&|'""`(){]*/)?(?:go)?pass(?:\s+(?:show|insert|add|edit|generate|rm|remove|delete|mv|rename|cp|copy|git|init|ls|list|find|search|grep|otp)(?=[ \t]|$|[\r\n;&|<>'""`)])|\s+-\S|\s+(?:(?<q>['""])[^'""\r\n]+\k<q>|\$\([^()\r\n]*\)|(?![0-9]+(?:[\s;&|'""`()<>]|$))[^\s;&|'""`()<>]+)(?=[ \t]*(?:[0-9]?[<>]|$|[\r\n;&|'""`)])|[ \t]+#)(?(cell)(?![ \t]*\|(?!\|))))", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejPassRegex();

    // A command line that cannot be read for certain keeps the raw shape's verdict, failing closed.
    private static bool RunsPasswordStore(string command)
    {
        if (!OndrejPassRegex().IsMatch(command))
            return false;
        var (found, uncertain) = Runs(command, words => ProgramCall(words) is { Program: "pass" or "gopass" });
        return found || uncertain;
    }

    [GeneratedRegex(@"\bpy(?:thon[0-9.]*)?w?(?:\.exe)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex PythonWordRegex();

    // python -, the form a command line that cannot be read for certain is still refused for.
    [GeneratedRegex(@"\bpy(?:thon[0-9.]*)?w?(?:\.exe)?(?:\s+-\S+)*\s+-(?=\s|$|[;&|)])", RegexOptions.IgnoreCase)]
    private static partial Regex PythonDashRegex();

    [GeneratedRegex(@"^py(?:thon[0-9.]*)?w?$")]
    private static partial Regex PythonProgramRegex();

    private static readonly HashSet<string> PythonInfoFlags = new(StringComparer.Ordinal)
        { "-V", "-VV", "--version", "-h", "-?", "--help", "--help-env", "--help-xoptions", "--help-all", "--list", "--list-paths", "-0", "-0p" };

    // Where a Python call takes its program from, when not from a script, -c or -m.
    private enum PythonSource { None, Dash, NoOperand }

    /// <summary>
    /// A Python program is read from stdin when a Python call names - as its program, or names none
    /// while something feeds its stdin: a pipe, a &lt; redirection, a heredoc or a here-string.
    /// </summary>
    private static bool RunsPythonFromStdin(string command)
    {
        if (!PythonWordRegex().IsMatch(command))
            return false;
        var (dash, uncertain) = Runs(command, words => PythonSourceOf(words) == PythonSource.Dash);
        return dash || (uncertain && PythonDashRegex().IsMatch(command))
            || CommandLines(command, ShellDialect.Unknown).Lines.Select(line => line.Text).Distinct().SelectMany(FedStages)
                .Any(stage => Runs(stage, words => PythonSourceOf(words) == PythonSource.NoOperand).Found);
    }

    /// <summary>
    /// Where a Python call (python, python3.12, pythonw or py, under any path, case or .exe) takes its
    /// program: <c>Dash</c> when its first operand is -, <c>NoOperand</c> when it has none, so it reads
    /// stdin when something feeds it and prompts otherwise. A script operand, -c or -m (a short option
    /// cluster naming c or m), a help or version flag, operands xargs supplies, a lookup by
    /// command -v or -V, and any other command are <c>None</c>.
    /// </summary>
    private static PythonSource PythonSourceOf(IReadOnlyList<string> words)
    {
        var at = CommandPosition.Skip(words);
        if (at >= words.Count || !PythonProgramRegex().IsMatch(CommandPosition.ProgramName(words[at])) || LooksUp(words, at))
            return PythonSource.None;

        for (var i = at + 1; i < words.Count; i++)
        {
            var word = words[i];
            if (word == "-")
                return PythonSource.Dash;
            if (word.Length < 2 || word[0] != '-' || PythonInfoFlags.Contains(word)
                || (word[1] != '-' && word.AsSpan(1).IndexOfAny('c', 'm') >= 0))
                return PythonSource.None;
            if (word is "-W" or "-X" or "--check-hash-based-pycs")
                i++;
        }

        return words.Take(at).Any(word => CommandPosition.ProgramName(word) == "xargs") ? PythonSource.None : PythonSource.NoOperand;
    }

    // command -v or -V before the program only names it.
    private static bool LooksUp(IReadOnlyList<string> words, int program) =>
        words.Take(program).SkipWhile(word => CommandPosition.ProgramName(word) != "command").Skip(1)
            .TakeWhile(word => word.StartsWith('-')).Any(word => word.AsSpan(1).IndexOfAny('v', 'V') >= 0);

    // A command separator in a layout: a list operator, a pipe (| or |&), an & that is no
    // redirection, a parenthesis or brace, or a line break.
    [GeneratedRegex(@"\|\||&&|\|&?|(?<![<>])&(?!>)|[;\n(){}]")]
    private static partial Regex SeparatorRegex();

    /// <summary>
    /// The commands of one command line whose stdin something feeds: each after a pipe, and each
    /// holding a &lt; redirection, a heredoc or a here-string. Bodies a command only reads are left
    /// out, and a separator inside quotes splits nothing.
    /// </summary>
    private static IEnumerable<string> FedStages(string text)
    {
        var code = StripLiteralBodies(text);
        var layout = QuotedTextRegex().Replace(code, quoted => new string(' ', quoted.Length));
        var start = 0;
        var piped = false;
        foreach (Match separator in SeparatorRegex().Matches(layout + "\n"))
        {
            if (piped || layout.AsSpan(start, separator.Index - start).Contains('<'))
                yield return code[start..separator.Index];
            piped = separator.Value is "|" or "|&";
            start = separator.Index + separator.Length;
        }
    }

    private const int MaxCommandLineDepth = 8;

    /// <summary>One reading of one command line: its text and its commands' words.</summary>
    private sealed record CommandLine(string Text, List<IReadOnlyList<string>> Commands);

    /// <summary>
    /// The command lines a command line runs, read as its shell reads them (<see cref="ShellReader"/>;
    /// both ways when the shell is unknown): one per reading of each. Quotes, comments, heredoc and
    /// here-string bodies are no commands. The command string a shell, eval or env -S is given
    /// (<see cref="CommandPosition.ScriptOf"/>), the heredoc or here-string a shell given none reads,
    /// and a bash backtick substitution are command lines of their own. <c>Uncertain</c> when a
    /// reading cannot finish, a command string is encoded, a shell reads a pipe the line does not
    /// show, or nesting passes the limit.
    /// </summary>
    private static (List<CommandLine> Lines, bool Uncertain) CommandLines(string command, ShellDialect dialect)
    {
        List<CommandLine> lines = [];
        var uncertain = ReadCommandLines(command, dialect, 0, lines);
        return (lines, uncertain);
    }

    private static bool ReadCommandLines(string text, ShellDialect dialect, int depth, List<CommandLine> lines)
    {
        if (depth > MaxCommandLineDepth)
            return true;

        var uncertain = false;
        bool[] readings = dialect switch
        {
            ShellDialect.Posix => [true],
            ShellDialect.PowerShell => [false],
            _ => [true, false],
        };
        foreach (var posix in readings)
        {
            var reading = ShellReader.Read(text, posix);
            lines.Add(new CommandLine(text, [.. reading.Commands.Select(command => (IReadOnlyList<string>)command.Words)]));
            uncertain |= reading.Failed;
            foreach (var substitution in reading.Substitutions)
                uncertain |= ReadCommandLines(substitution, ShellDialect.Posix, depth + 1, lines);
            foreach (var command in reading.Commands)
                uncertain |= ReadRunLines(command, depth, lines);
        }

        return uncertain;
    }

    // The command lines one command runs: its command string, or the stdin bodies a shell reads.
    private static bool ReadRunLines(ShellReader.Command command, int depth, List<CommandLine> lines)
    {
        if (ProgramCall(command.Words) is not { } call)
            return false;
        if (CommandPosition.ScriptOf(call.Program, call.Arguments) is { } script)
            return script.Encoded || ReadCommandLines(script.Text, script.Dialect, depth + 1, lines);
        if (StdinShellDialect(call.Program, call.Arguments) is not { } dialect)
            return false;

        var uncertain = command.Stdin.Count == 0;
        foreach (var body in command.Stdin)
            uncertain |= ReadCommandLines(body, dialect, depth + 1, lines);
        return uncertain;
    }

    // The dialect of a shell that reads its commands from stdin, or null for any other program.
    private static ShellDialect? StdinShellDialect(string program, List<string> rest)
    {
        if (CommandPosition.PosixShells.Contains(program))
            return CommandPosition.PosixShellString(rest) == null ? ShellDialect.Posix : null;
        if (program is not ("pwsh" or "powershell"))
            return null;
        return CommandPosition.ReadPwsh(rest) is (CommandPosition.PwshMode.Command, [] or ["-"]) or (CommandPosition.PwshMode.Positional, [])
            ? ShellDialect.PowerShell
            : null;
    }

    /// <summary>A command's program name and the words after it, past assignments and runners; null when it has none.</summary>
    private static (string Program, List<string> Arguments)? ProgramCall(IReadOnlyList<string> words)
    {
        var at = CommandPosition.Skip(words);
        return at < words.Count ? (CommandPosition.ProgramName(words[at]), words.Skip(at + 1).ToList()) : null;
    }

    // Whether a command the line runs, read both ways, meets isCall on its words, and whether the reading was certain.
    private static (bool Found, bool Uncertain) Runs(string command, Func<IReadOnlyList<string>, bool> isCall)
    {
        var (lines, uncertain) = CommandLines(command, ShellDialect.Unknown);
        return (lines.SelectMany(line => line.Commands).Any(isCall), uncertain);
    }

    // Written where a nudge's match starts; no command line holds it.
    private const string LaunchMark = "\u0001";

    /// <summary>
    /// Whether a program named at <paramref name="index"/> of <paramref name="command"/> would run
    /// (DYD-406): with a mark written there, the line read as its shell reads it has the mark in a
    /// program word, past assignments and runners, rather than in an argument, a comment or a body a
    /// command reads. A shell's command string is read as commands. A line that cannot be read for
    /// certain counts, failing closed.
    /// </summary>
    internal static bool RunsAt(string command, int index, ShellDialect dialect)
    {
        var (lines, uncertain) = CommandLines(command.Insert(index, LaunchMark), dialect);
        return uncertain || lines.SelectMany(line => line.Commands)
            .Any(words => CommandPosition.Skip(words) is var at && at < words.Count && words[at].Contains(LaunchMark, StringComparison.Ordinal));
    }

    [GeneratedRegex(CommandStart + @"op\s+(?:read|run|inject|item|document|vault|connect|service-account|events-api|signin)" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejOpRegex();

    [GeneratedRegex(CommandStart + @"security\s+(?:(?:-[a-z]+|--[a-z-]+)\s+)*(?:find-generic-password|find-internet-password|dump-keychain)" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejKeychainRegex();

    [GeneratedRegex(@"--export-secret-(?:key|keys|subkey|subkeys)(?=\s|=|$)", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejGpgSecretRegex();

    [GeneratedRegex(@"(?:~|\$HOME|\$\{HOME\}|/Users/[^/\s'""]+)/\.password-store(?:/|(?=\s|$|['"";&|]))", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejPasswordStorePathRegex();

    [GeneratedRegex(CommandStart + @"(?:open\s+-a\s+['""]?(?:1Password|Bitwarden|NordPass|KeePass)|brew\s+(?:uninstall|remove|rm)\b[^;&|\r\n]*\s['""]?(?:1password|bitwarden(?:-cli)?|nordpass|keepassxc|lastpass))(?=\s|$|['""/;&|])|(?<=^|[/\s'""])(?:1Password|Bitwarden|NordPass|KeePassXC)\.app(?=\s|$|['""/;&|])", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex OndrejPasswordAppRegex();

    private const string WorktreeRemovalReason =
        "Raw worktree removal can lose uncommitted work or follow a link out of the tree. "
        + "From outside the tree run dydo clean --dry-run <worktree>, then dydo clean <worktree>";

    // One git -C/-c value: double- or single-quoted (spaces allowed), or a bare word.
    private const string GitOptionValue = @"(?:""[^""\r\n]*""|'[^'\r\n]*'|[^\s;&|'""]+)";

    // git worktree remove with a force flag (-f, git's repeated -ff, --force), after any git -C/-c
    // options, quoted or not. Plain remove refuses a dirty tree, so it stays allowed.
    [GeneratedRegex(CommandStart + @"git(?:\s+-[cC]\s+" + GitOptionValue + @")*\s+worktree\s+remove\b[^;&|\r\n]*\s(?:-f+|--force)" + CommandEnd, RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex ForcedWorktreeRemoveRegex();

    // A worktree path argument: an optional prefix ending in a separator, then a worktrees folder, one
    // entry in it, or T3's .t3/worktrees/<project>/<thread>, then optional trailing separators.
    private const string BareWorktreeArgument =
        @"\s(?:[^\s'""`;&|]*[/\\])?(?:worktrees(?:[/\\]+[^\s/\\'""`;&|]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^\s/\\'""`;&|]+){2})[/\\]*(?=[\s'""`);&|]|$)";

    private const string DoubleQuotedWorktreeArgument =
        @"""(?:[^""\r\n]*[/\\])?(?:worktrees(?:[/\\]+[^/\\""\r\n]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^/\\""\r\n]+){2})[/\\]*""";

    private const string SingleQuotedWorktreeArgument =
        @"'(?:[^'\r\n]*[/\\])?(?:worktrees(?:[/\\]+[^/\\'\r\n]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^/\\'\r\n]+){2})[/\\]*'";

    // A recursive delete of a worktree itself: the argument is a worktrees folder or one entry in it
    // (T3's .t3/worktrees/<project>/<thread> counts two deep), so a delete inside a tree, such as its
    // bin or obj, stays allowed. Recursive means rm -r/-R/--recursive (flag clusters of rm's own
    // letters, so -f alone and PowerShell's -Force do not count), PowerShell -Recurse and its prefixes,
    // and cmd's rd /s. The argument is bare (a name ends at whitespace) or quoted (a name may hold
    // spaces and ends at the closing quote). An accident guard, not a shell parser: a worktree outside
    // a worktrees folder is not recognised.
    [GeneratedRegex(CommandStart + @"(?:rm|rmdir|rd|del|erase|ri|Remove-Item)(?=\s)(?=[^;&|\r\n]*\s(?:-[dfiIvrR]*[rR][dfiIvrR]*|--recursive|-rec(?:u(?:r(?:s(?:e)?)?)?)?|/s)(?=[\s:]|$))(?=[^;&|\r\n]*(?:" + BareWorktreeArgument + "|" + DoubleQuotedWorktreeArgument + "|" + SingleQuotedWorktreeArgument + "))", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex RecursiveWorktreeDeleteRegex();

    // Matches inline interpreter execution: python -c, node -e, ruby -e, perl -e/-E, php -r.
    // Does NOT include bash/sh/zsh -c - those are handled by shell -c subcommand extraction
    // in AnalyzeSubCommand, which analyzes the inner command's file operations instead of
    // blocking outright (blocking all bash -c would cause massive false positives).
    // Does NOT match script file execution (python script.py) or version flags (python --version).
    [GeneratedRegex(@"\b(?:python[23]?|node|ruby|perl)\s+(?:-\w+\s+)*-[ceE]\s|\bphp\s+(?:-\w+\s+)*-r\s", RegexOptions.IgnoreCase, DangerousPatternTimeoutMs)]
    private static partial Regex InlineInterpreterRegex();

    // Coaching: detect needless change-directory + command compounds.
    // Covers Bash `cd` and PowerShell forms (Set-Location, sl, chdir, Push-Location, pushd) -
    // all break Claude Code auto-approve when chained (`cd dir && cmd`, `Set-Location dir; cmd`).
    [GeneratedRegex(@"^\s*(?:cd|Set-Location|sl|chdir|Push-Location|pushd)\s+(?:""([^""]+)""|'([^']+)'|(\S+))\s*(?:&&|;)\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex CdThenCommandRegex();

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

        var cdPath = match.Groups[1].Value;
        if (!match.Groups[1].Success)
            cdPath = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

        return (true, cdPath, match.Groups[4].Value.Trim());
    }

    public BashAnalysisResult Analyze(string command)
    {
        var result = new BashAnalysisResult();

        if (string.IsNullOrWhiteSpace(command))
            return result;

        var (isDangerous, reason) = CheckDangerousPatterns(command);
        if (isDangerous)
        {
            result.HasDangerousPattern = true;
            result.DangerousPatternReason = reason;
            return result;
        }

        // Strip cat heredoc blocks - literal text, not shell code
        var strippedCommand = CatHeredocRegex().Replace(command, "HEREDOC_STRIPPED");

        // Literal data - a quoted heredoc, an @' here-string, single-quoted text - is neither
        // expansion nor code, so only the expanding text is searched for bypasses (DYD-406).
        var analysed = StripLiteralBodies(strippedCommand);
        var expanding = ExpandingText(strippedCommand);
        CheckBypassAttempts(expanding, Layout(analysed), result);

        var subCommands = SplitCommand(analysed);

        foreach (var subCmd in subCommands)
        {
            AnalyzeSubCommand(subCmd.Trim(), result);
        }

        // Analyze contents of command substitutions so hidden operations are detected
        if (result.HasBypassAttempt)
            AnalyzeCommandSubstitutions(expanding, result);

        return result;
    }

    private void AnalyzeCommandSubstitutions(string command, BashAnalysisResult result)
    {
        foreach (Match match in CommandSubstitutionRegex().Matches(command))
        {
            var raw = match.Value;
            string inner;
            if (raw.StartsWith("$(") && raw.EndsWith(")"))
                inner = raw[2..^1];
            else if (raw.StartsWith("`") && raw.EndsWith("`"))
                inner = raw[1..^1];
            else
                continue;

            foreach (var subCmd in SplitCommand(inner))
                AnalyzeSubCommand(subCmd.Trim(), result);
        }
    }

    public (bool IsDangerous, string? Reason) CheckDangerousPatterns(string command)
    {
        try
        {
            foreach (var (isMatch, reason) in DangerousPatterns)
            {
                if (isMatch(command))
                    return (true, reason);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return (true, "Command too complex to verify safely");
        }
        return (false, null);
    }

    // Each check reads the expanding text; a line break counts only in the layout, where it separates
    // commands rather than sitting in quoted text or a body.
    private static readonly (Func<string, string, bool> Check, string Warning)[] BypassChecks =
    [
        ((code, _) => CommandSubstitutionRegex().IsMatch(code), "Command contains command substitution - paths may be dynamic"),
        ((code, _) => Base64DecodeRegex().IsMatch(code), "Command contains base64 decode - potential obfuscation"),
        ((code, _) => HexDecodeRegex().IsMatch(code), "Command contains hex decode - potential obfuscation"),
        ((code, _) => VariableExpansionRegex().IsMatch(code), "Command contains variable expansion - paths may be dynamic"),
        ((code, layout) => layout.Contains('\n') || code.Contains("$'\\n'") || code.Contains("%0a"), "Command contains embedded newlines"),
    ];

    private static void CheckBypassAttempts(string expanding, string layout, BashAnalysisResult result)
    {
        foreach (var (check, warning) in BypassChecks)
        {
            if (check(expanding, layout))
            {
                result.Warnings.Add(warning);
                result.HasBypassAttempt = true;
            }
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
        if (c == '|')
            return 1;
        return 0;
    }

    private static bool AppendQuote(char c, StringBuilder current,
        ref bool inSingleQuote, ref bool inDoubleQuote)
    {
        if (c == '\'' && !inDoubleQuote)
            inSingleQuote = !inSingleQuote;
        else if (c == '"' && !inSingleQuote)
            inDoubleQuote = !inDoubleQuote;
        else
            return false;

        current.Append(c);
        return true;
    }

    // How many characters at i an unquoted separator or, with dropComments, a comment consumes;
    // 0 when neither starts there. A comment stops before its newline, so the newline still splits.
    private static int ConsumeUnquoted(string command, int i, StringBuilder current,
        List<string> parts, bool dropComments)
    {
        if (dropComments && command[i] == '#' && (current.Length == 0 || char.IsWhiteSpace(current[^1])))
        {
            var newline = command.IndexOf('\n', i);
            return (newline < 0 ? command.Length : newline) - i;
        }

        var sepLen = CheckCommandSeparator(command, i);
        if (sepLen > 0 && current.Length > 0)
        {
            parts.Add(current.ToString());
            current.Clear();
        }
        return sepLen;
    }

    /// <summary>
    /// Splits a command at unquoted separators. A backslash is literal inside single quotes. With
    /// <paramref name="dropComments"/>, an unquoted <c>#</c> that begins a word drops the rest of
    /// its line, as a shell comment.
    /// </summary>
    internal static IEnumerable<string> SplitCommand(string command, bool dropComments = false)
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

            if (c == '\\' && !inSingleQuote)
            {
                escapeNext = true;
                current.Append(c);
                continue;
            }

            if (AppendQuote(c, current, ref inSingleQuote, ref inDoubleQuote))
                continue;

            if (!inSingleQuote && !inDoubleQuote)
            {
                var consumed = ConsumeUnquoted(command, i, current, parts, dropComments);
                if (consumed > 0)
                {
                    i += consumed - 1;
                    continue;
                }
            }

            current.Append(c);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    private static readonly HashSet<string> ShellInterpreters =
        new(StringComparer.OrdinalIgnoreCase) { "bash", "sh", "zsh" };

    private void AnalyzeSubCommand(string command, BashAnalysisResult result)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        if (ReadAssignmentSource(command) is { } source)
        {
            AnalyzeSubCommand(source, result);
            return;
        }

        var tokens = TokenizeCommand(command);
        if (tokens.Count == 0)
            return;

        var cmdName = tokens[0];

        if (TryAnalyzeShellC(tokens, result))
            return;

        AnalyzeSedCommand(cmdName, tokens, result);
        AnalyzeAwkCommand(cmdName, tokens, result);

        // Redirection operators apply to all commands
        AnalyzeRedirection(command, result);

        if (AllCommands.TryGetValue(cmdName, out var opType))
            AddPathOperations(result, tokens.Skip(1), opType, cmdName);
        else
            AnalyzeUncertainCommand(cmdName, tokens, result);
    }

    [GeneratedRegex(@"^\$[A-Za-z_]\w*\s*=\s*(?<source>\S.*)$", RegexOptions.Singleline)]
    private static partial Regex VariableAssignmentRegex();

    private static readonly HashSet<string> ContentReaders = new(StringComparer.OrdinalIgnoreCase)
        { "get-content", "gc", "cat", "type", "select-string" };

    /// <summary>
    /// The read a PowerShell assignment makes (<c>$p = Get-Content notes.md</c>, spaced or not): its
    /// right side when that is a content reader, read on its own. Any other right side stays an unknown
    /// <c>$</c> command, flagged as an uncertain write. No PowerShell evaluation beyond this.
    /// </summary>
    private static string? ReadAssignmentSource(string command)
    {
        var assignment = VariableAssignmentRegex().Match(command);
        if (!assignment.Success)
            return null;

        var source = assignment.Groups["source"].Value;
        return TokenizeCommand(source) is [var reader, ..] && ContentReaders.Contains(reader) ? source : null;
    }

    private bool TryAnalyzeShellC(List<string> tokens, BashAnalysisResult result)
    {
        if (!ShellInterpreters.Contains(tokens[0]))
            return false;

        var cArgIndex = FindShellCArgument(tokens);
        if (cArgIndex < 0 || cArgIndex + 1 >= tokens.Count)
            return false;

        var innerCommand = tokens[cArgIndex + 1];
        foreach (var subCmd in SplitCommand(innerCommand))
            AnalyzeSubCommand(subCmd.Trim(), result);
        AnalyzeRedirection(innerCommand, result);
        return true;
    }

    private static void AnalyzeSedCommand(string cmdName, List<string> tokens, BashAnalysisResult result)
    {
        if (!cmdName.Equals("sed", StringComparison.OrdinalIgnoreCase))
            return;

        if (tokens.Any(t => t == "-i" || t.StartsWith("-i") || t == "--in-place"))
        {
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
            foreach (var file in ExtractPaths(tokens.Skip(1)))
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

    private static void AnalyzeAwkCommand(string cmdName, List<string> tokens, BashAnalysisResult result)
    {
        if (!cmdName.Equals("awk", StringComparison.OrdinalIgnoreCase) &&
            !cmdName.Equals("gawk", StringComparison.OrdinalIgnoreCase))
            return;

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

    /// <summary>
    /// When the command name is a variable ($CMD) or substitution (`cmd`), the actual
    /// command is unknown - flag path-like arguments as uncertain writes.
    /// </summary>
    private static void AnalyzeUncertainCommand(string cmdName, List<string> tokens, BashAnalysisResult result)
    {
        if (!cmdName.StartsWith('$') && !cmdName.StartsWith('`'))
            return;

        foreach (var path in ExtractPaths(tokens.Skip(1)))
        {
            result.Operations.Add(new FileOperation
            {
                Type = FileOperationType.Write,
                Path = path,
                Command = cmdName,
                IsUncertain = true
            });
        }
    }

    private static int FindShellCArgument(List<string> tokens)
    {
        for (int i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token == "-c")
                return i;
            // Combined flags: -xc, -exc, etc. - if starts with - (not --) and contains 'c'
            if (token.Length > 1 && token[0] == '-' && token[1] != '-' && token.Contains('c'))
                return i;
        }
        return -1;
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
        var args = tokens.ToList();
        var paths = ExtractPaths(args).ToList();
        var destination = type == FileOperationType.Copy ? FindCopyDestination(args, paths, cmdName) : null;
        if (destination != null && !paths.Contains(destination))
            paths.Add(destination);

        foreach (var path in paths)
        {
            result.Operations.Add(new FileOperation
            {
                Type = type,
                Path = path,
                Command = cmdName,
                IsCopySource = destination != null && path != destination
            });
        }
    }

    // The flags that name a copy's destination, per command. Without one, a copy writes its last
    // path; robocopy its second (`robocopy <src> <dst> [files]`).
    private static readonly Dictionary<string, string[]> CopyTargetFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cp"] = ["-t", "--target-directory"],
        ["copy-item"] = ["-destination"],
        ["ci"] = ["-destination"],
        ["cpi"] = ["-destination"],
    };

    private static string? FindCopyDestination(List<string> args, List<string> paths, string cmdName)
    {
        var flags = CopyTargetFlags.GetValueOrDefault(cmdName, []);
        for (var i = 0; i < args.Count; i++)
        {
            var flagged = FlagValue(args, i, flags);
            if (flagged != null)
                return flagged;
        }

        var index = cmdName.Equals("robocopy", StringComparison.OrdinalIgnoreCase) ? 1 : paths.Count - 1;
        return paths.ElementAtOrDefault(index);
    }

    private static string? FlagValue(List<string> args, int i, string[] flags)
    {
        foreach (var flag in flags)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return args.ElementAtOrDefault(i + 1);
            if (args[i].StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(flag.Length + 1)..];
        }
        return null;
    }

    private static void AnalyzeRedirection(string command, BashAnalysisResult result)
    {
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
