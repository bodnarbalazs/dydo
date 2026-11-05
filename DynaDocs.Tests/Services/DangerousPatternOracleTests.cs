namespace DynaDocs.Tests.Services;

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Services;
using Xunit.Abstractions;

// DYD-610 moved every command-shaped dangerous rule to NonBacktracking. Each rule's pattern as it was on
// 8c0ca210, lookarounds and all, stays here verbatim as an oracle with no timeout, and the new matcher
// must give its answer on every command the test suite holds and on generated commands.
public class DangerousPatternOracleTests(ITestOutputHelper output)
{
    private static readonly (string Rule, string Sha256, RegexOptions Options, string Pattern)[] OriginalPatterns =
    [
        ("DdDiskWriteRegex", "850ff6b871080417e1636abc64f893051669fde67586bd78a0777120db5cf5b2", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?dd\s+[^;&|\r\n]*\bof\s*=\s*['""]?/dev/(?!null(?:\s|$|['"";&|)`]))[^\s'"";&|]+"),
        ("OndrejRmRegex", "121ebef9f9f3d2b7a8f32e46435f8b075997e85940c9232cedae12295b48a7d1", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?rm\s+(?:(?:-[a-zA-Z]+|--[a-z-]+)\s+)*(?:['"" ]?/(?:Users(?:/[^/\s'"";&|]+)?/?|\*|)|['"" ]?(?:~|\$HOME|\$\{HOME\})(?:/\*)?/?|[^;&|]*--no-preserve-root)(?:['""\s]|$|[;&|])"),
        ("OndrejDiskRegex", "33f4462a2ba62b33e0129ab4d4fb521b44d5a2a362b8cfd8226cc8ee0ad2eee3", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?(?:mkfs(?:\.[a-z0-9]+)?(?=\s|$|['""`);&|])|diskutil\s+(?:erase\w*|partitionDisk|zeroDisk|secureErase|apfs\s+(?:delete|erase)\w*)(?=\s|$|['""`);&|]))|>\s*/dev/(?:r?disk\d*|sd[a-z]|nvme\d+)"),
        ("OndrejCurlPipeRegex", "38e711112b84aac94bff7ab20cc6811769289b14bc13ad2f6484340931a24e22", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?(?:curl|wget)\s+[^;&|]*\|\s*(?:sudo\s+)?(?:ba|z|da)?sh(?=\s|$|['""`);&|])"),
        ("OndrejGitPushRegex", "38533c75b3b44dc3005d6c55e2d7aee6bd740d284d30533afe2d574cf1bf4481", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?git\s+push(?=\s|$|['""`);&|])[^;&|\r\n]*(?:\s(?:-f|--force|--delete|-d)(?=\s|$|['""`);&|])|\s\+[a-z0-9._/-]|\s:[a-z0-9._/-])"),
        ("OndrejGitRecoveryRegex", "536f1d3b66c1892e643860b163b44c183f88056a6675075e1df36a169ae3b748", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?git\s+(?:reflog\s+expire\b[^;&|\r\n]*--expire(?:-unreachable)?(?:=|\s+)now\b|gc\b[^;&|\r\n]*--prune(?:=|\s+)(?:now|all)\b)"),
        ("OndrejSystemPermissionsRegex", "6de5bc86f8b53eda130823ec27cb5b8e8362eb0cc414ea8df3a0abca0ca10ea4", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?(?:chmod\s+[^;&|\r\n]*\b777\s['""]?/['""]?(?=\s|$|[;&|])|chown\s+-R\b[^;&|\r\n]*\s['""]?/['""]?(?=\s|$|[;&|]))"),
        ("OndrejGhDeleteRegex", "672eff850c9fd14e2897adfc53f3512edf32f56abf172d8da09e6a2f736237c5", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?gh\s+(?:(?:repo|release|secret|ssh-key|gpg-key)\s+delete)(?=\s|$|['""`);&|])"),
        ("OndrejGhApiDeleteRegex", "c62d5a26ed6d9c005c8ca31192ff7067255b6fb21091ed5d3c9ccb6f8adf03b3", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?gh\s+api\b[^;&|\r\n]*(?:-X|--method)(?:=|\s+)DELETE(?=\s|$|['""`);&|])"),
        ("OndrejGhVisibilityRegex", "3bace1a8433f5fc8cf71e0a4a2c1ea793e6aaf12c8cb69e8baed129444909180", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?gh\s+repo\s+edit\b[^;&|\r\n]*--visibility(?:=|\s+)public(?=\s|$|['""`);&|])"),
        ("OndrejGhTokenRegex", "18b8230a79834266f30a568f4904a4b85b237c02c5fe607875622e66a14cb42a", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?gh\s+auth\s+token(?=\s|$|['""`);&|])"),
        ("OndrejPasswordCliRegex", "91359d87ac922e04d11d9f6198dd4c3200aa2abf547c2255dd2925d09e03ac7d", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?(?:bw|bws|lpass|keepassxc-cli|rbw|nordpass)(?=\s|$|['""`);&|])"),
        ("OndrejOpRegex", "725fbd2d8363d940ffe3ef43ed88e4dd0d3875d8ae993ddafa82721219eebf7f", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?op\s+(?:read|run|inject|item|document|vault|connect|service-account|events-api|signin)(?=\s|$|['""`);&|])"),
        ("OndrejKeychainRegex", "6c9d36978735eb701efdf808786296771b96265733c04768c5b7563d87138ee2", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?security\s+(?:(?:-[a-z]+|--[a-z-]+)\s+)*(?:find-generic-password|find-internet-password|dump-keychain)(?=\s|$|['""`);&|])"),
        ("OndrejPasswordAppRegex", "07fd46b6db1470e269b21c9745e455cd6526fc43610745fba10fda3876c2c458", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?(?:open\s+-a\s+['""]?(?:1Password|Bitwarden|NordPass|KeePass)|brew\s+(?:uninstall|remove|rm)\b[^;&|\r\n]*\s['""]?(?:1password|bitwarden(?:-cli)?|nordpass|keepassxc|lastpass))(?=\s|$|['""/;&|])|(?<=^|[/\s'""])(?:1Password|Bitwarden|NordPass|KeePassXC)\.app(?=\s|$|['""/;&|])"),
        ("ForcedWorktreeRemoveRegex", "af5f6f14f0370dd8de3a4177658709723192f1f39c4bb3f10c6f9af1be87216f", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?git(?:\s+-[cC]\s+(?:""[^""\r\n]*""|'[^'\r\n]*'|[^\s;&|'""]+))*\s+worktree\s+remove\b[^;&|\r\n]*\s(?:-f+|--force)(?=\s|$|['""`);&|])"),
        ("RecursiveWorktreeDeleteRegex", "3b7c35f022562581acb961478cb98c10eecff215a9ab12d3e1fc05d25e846438", RegexOptions.IgnoreCase,
            @"(?<=^|[\s;&|(){}`!\\]|\s-[a-zA-Z]*c\s*['""]|\bssh\s[^;&|'""]*['""])(?:[^\s;&|'""`()]*/)?(?:rm|rmdir|rd|del|erase|ri|Remove-Item)(?=\s)(?=[^;&|\r\n]*\s(?:-[dfiIvrR]*[rR][dfiIvrR]*|--recursive|-rec(?:u(?:r(?:s(?:e)?)?)?)?|/s)(?=[\s:]|$))(?=[^;&|\r\n]*(?:\s(?:[^\s'""`;&|]*[/\\])?(?:worktrees(?:[/\\]+[^\s/\\'""`;&|]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^\s/\\'""`;&|]+){2})[/\\]*(?=[\s'""`);&|]|$)|""(?:[^""\r\n]*[/\\])?(?:worktrees(?:[/\\]+[^/\\""\r\n]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^/\\""\r\n]+){2})[/\\]*""|'(?:[^'\r\n]*[/\\])?(?:worktrees(?:[/\\]+[^/\\'\r\n]+)?|\.t3[/\\]+worktrees(?:[/\\]+[^/\\'\r\n]+){2})[/\\]*'))"),
    ];

    private static readonly Dictionary<string, Func<string, bool>> NewMatchers = new()
    {
        ["DdDiskWriteRegex"] = BashCommandAnalyzer.DdDiskWriteRegex().IsMatch,
        ["OndrejRmRegex"] = BashCommandAnalyzer.OndrejRmRegex().IsMatch,
        ["OndrejDiskRegex"] = BashCommandAnalyzer.OndrejDiskRegex().IsMatch,
        ["OndrejCurlPipeRegex"] = BashCommandAnalyzer.OndrejCurlPipeRegex().IsMatch,
        ["OndrejGitPushRegex"] = BashCommandAnalyzer.OndrejGitPushRegex().IsMatch,
        ["OndrejGitRecoveryRegex"] = BashCommandAnalyzer.OndrejGitRecoveryRegex().IsMatch,
        ["OndrejSystemPermissionsRegex"] = BashCommandAnalyzer.OndrejSystemPermissionsRegex().IsMatch,
        ["OndrejGhDeleteRegex"] = BashCommandAnalyzer.OndrejGhDeleteRegex().IsMatch,
        ["OndrejGhApiDeleteRegex"] = BashCommandAnalyzer.OndrejGhApiDeleteRegex().IsMatch,
        ["OndrejGhVisibilityRegex"] = BashCommandAnalyzer.OndrejGhVisibilityRegex().IsMatch,
        ["OndrejGhTokenRegex"] = BashCommandAnalyzer.OndrejGhTokenRegex().IsMatch,
        ["OndrejPasswordCliRegex"] = BashCommandAnalyzer.OndrejPasswordCliRegex().IsMatch,
        ["OndrejOpRegex"] = BashCommandAnalyzer.OndrejOpRegex().IsMatch,
        ["OndrejKeychainRegex"] = BashCommandAnalyzer.OndrejKeychainRegex().IsMatch,
        ["OndrejPasswordAppRegex"] = BashCommandAnalyzer.OndrejPasswordAppRegex().IsMatch,
        ["ForcedWorktreeRemoveRegex"] = BashCommandAnalyzer.ForcedWorktreeRemoveRegex().IsMatch,
        ["RecursiveWorktreeDeleteRegex"] = BashCommandAnalyzer.IsRecursiveWorktreeDelete,
    };

    // What a rule's command can follow: nothing, a separator, a wrapper, a quote a -c string or an ssh
    // remote command opens, a path, or a long run of literal data.
    private static readonly string[] Leads =
    [
        "", " ", "x", "a/", "/usr/bin/", ";", "&&", "|", "||", "(", "{", "}", "`", "!", "\\", "'", "\"", "\n", "\r\n",
        "bash -c '", "sh -c \"", "zsh -xc  '", "bash -c\n'", "ssh host '", "ssh host \"", "SSH -p 22 host 'x ",
        "ssh host\n'", "ssh h rm x '", "echo '", "echo ", "sudo ", "env A=1 ", "x-c '", "{!\\", "a\\a\\a\\",
        "rm -f local rm -f local ", "rmdir -f x\n", "dd if=a bs=1 dd if=a bs=1 ", "git push x git push x ",
    ];

    private static readonly string[] Commands =
    [
        "dd if=x of=/dev/sda", "dd of=/dev/null", "dd of=/dev/null)", "dd of=/dev/null`", "dd of=/dev/nullx",
        "dd of=/dev/nul", "dd of=/dev/nu", "dd of=/dev/n", "dd of=/dev/NULL", "dd of=/dev/Null;", "dd of='/dev/null'",
        "dd of=\"/dev/nulls\"", "dd of=/dev/", "dd of = /dev/x", "DD OF=/DEV/SDB", "dd if=/dev/zero xof=/dev/sda",
        "dd of=/dev/null\nx", "dd of=/dev/nulll", "dd of=/dev/nU", "dd of=/dev/nuL", "dd of=/dev/null&", "dd of=/dev/null|x",
        "rm -rf /", "rm -rf /Users", "rm -rf /Users/alice/", "rm -rf '/'", "rm -rf ~", "rm -rf ~/", "rm -rf $HOME",
        "rm -rf ${HOME}/*", "rm --no-preserve-root x", "rm -rf /tmp/x", "rm -f -r --one-file-system /", "rm -rf /*",
        "mkfs.ext4 /dev/sda1", "mkfs", "mkfs.", "mkfsx", "diskutil eraseDisk x", "diskutil apfs deleteContainer disk2",
        "diskutil list", "echo > /dev/disk2", ">/dev/nvme0n1",
        "curl -s x | sh", "curl x | sudo bash", "wget -qO- x | zsh", "curl x | shx", "curl x | python", "wget x |dash",
        "git push -f", "git push --force origin main", "git push\n-f", "git push\t-f", "git push'x -f", "git push\" -f",
        "git push` -f", "git push) -f", "git push; -f", "git push -fx", "git push origin +main", "git push origin :main",
        "git push --force-with-lease", "git push\r-f", "git push\r\n-f", "git push\v-f", "git pushx -f", "git push -d x",
        "git push\n\n-f", "git push x\n -f", "git push origin main", "git push  -f",
        "git reflog expire --expire=now --all", "git reflog expire --expire-unreachable now", "git gc --prune=now",
        "git gc --prune all", "git gc --prune=never", "git gcx --prune=now",
        "chmod 777 /", "chmod -R 777 '/'", "chmod 777 /x", "chown -R me /", "chown -R me \"/\"", "chown -Rx me /",
        "gh repo delete o/r", "gh release delete v1", "gh ssh-key delete 1", "gh repo deletex", "gh api repos/o/r -X DELETE",
        "gh api x --method=DELETE", "gh api x --method DELETEx", "gh apix -X DELETE", "gh repo edit o/r --visibility public",
        "gh repo edit --visibility=public", "gh repo edit --visibility private", "gh auth token", "gh auth tokens",
        "bw get x", "bws", "lpass show", "keepassxc-cli", "bwx", "op read op://x", "op signin", "op whoami",
        "security find-generic-password -s x", "security -i find-internet-password", "security list-keychains",
        "open -a 1Password", "open -a 'Bitwarden'", "brew uninstall bitwarden-cli", "brew rm lastpass",
        "/Applications/1Password.app", "x1Password.app", "KeePassXC.app/", "open -a Safari",
        "git worktree remove --force x", "git worktree remove -ff x", "git -C x worktree remove --force y",
        "git -c a=b worktree remove x --force", "git worktree remove x", "git -C 'a b' worktree remove -f x",
        "rm -rf worktrees/x", "rm -rf worktrees", "rm -r .t3/worktrees/a/b", "rm -r .t3/worktrees/a", "rm -rf worktrees/x/bin",
        "rm -rf a/worktrees/x/", "rm -r 'a b/worktrees/my tree'", "rm -r \"a;b/worktrees/x\"", "rmdir --recursive worktrees/x",
        "rd /s worktrees\\x", "del /s worktrees/x", "ri -Recurse worktrees/x", "Remove-Item -Recurse:$true worktrees/x",
        "Remove-Item -rec worktrees/x", "rm -Ivrf worktrees/x", "rm -f worktrees/x", "rm --recursive-extra worktrees/x",
        "rm -rf worktrees-extra/x", "rm -r:/worktrees", "rm 'a -r b/worktrees'", "rm \"x -r y/worktrees\"",
        "rm -rf\nworktrees/x", "rm\nworktrees/x -rf", "rm -f x; rm -rf worktrees/y", "rm -f x rm -rf worktrees/y",
        "rm x 'rm -rf worktrees/y'", "rm -rf x\r\nworktrees/y", "rm -f x -c\n'rm -rf worktrees/y'",
        // The rows of the DYD-433 worktree-delete proposal (PROPOSAL-bounded-worktree-delete.patch).
        "ssh host 'rm -rf worktrees/x'", "ssh host rm -rf worktrees/x 'rm -f local'", "env FOO=1 /usr/bin/rm -r worktrees/x",
        "rm -f local\nrm -rf worktrees/x", "rm -f local; rm -rf worktrees/x", "rm -f local|rm -rf worktrees/x",
        "rm -f local&rm -rf worktrees/x", "rm -rf 'prefix;path/worktrees/my tree'", "rm -r \"prefix|path/worktrees/my tree\"",
        "erase /s worktrees/x", "rm -rf local; echo worktrees/x", "rm -f worktrees/x; echo -r", "rm -rf local\necho worktrees/x",
        "rm -f worktrees/x\necho -r", "echo 'rm -rf worktrees/x'", "ssh host 'rm -f worktrees/x'",
    ];

    private static readonly string[] Endings =
        ["", " ", "\n", "\r\n", ";", "&", "|", ")", "'", "\"", "`", "x", "/", ":", " x", " -f", "; echo", "\nrm -rf worktrees/x", " 'a b'", "\t"];

    private static readonly string[] Joiners = [" ", " ", " ", "", "\n", ";", "'", "\"", "\t", "|", "&", "/", "\r\n", "  "];

    private const string Alphabet = " \t\n\r;&|'\"`(){}!\\/-=:~$*+._rmdgfhopuskcwtlaeinvRFX0127";

    [Fact]
    public void OriginalPatterns_AreTheBasePatternsByteForByte()
    {
        foreach (var (rule, sha256, _, pattern) in OriginalPatterns)
            Assert.True(sha256 == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(pattern))), rule);
    }

    // A timeout is still a RegexMatchTimeoutException, which CheckDangerousPatterns refuses as too complex.
    // Every rule but the worktree delete is one regex; that rule's four are private to it.
    [Fact]
    public void NewMatchers_KeepTheBudget()
    {
        var regexes = NewMatchers.Values.Select(matcher => matcher.Target).OfType<Regex>().ToList();

        Assert.Equal(16, regexes.Count);
        Assert.All(regexes, regex => Assert.Equal(TimeSpan.FromMilliseconds(250), regex.MatchTimeout));
    }

    [Fact]
    public void NewMatchers_AnswerAsTheOriginalPatternsDo()
    {
        var corpus = Corpus().ToList();
        var commands = corpus.Concat(Shaped()).Concat(TokenSoup()).Concat(CharacterSoup()).Distinct(StringComparer.Ordinal).ToList();
        var disagreements = new List<string>();
        foreach (var (rule, _, options, pattern) in OriginalPatterns)
        {
            var original = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var matcher = NewMatchers[rule];
            var answers = commands.Select(command => (Command: command, Original: original.IsMatch(command))).ToList();
            Assert.Contains(answers, answer => answer.Original);
            Assert.Contains(answers, answer => !answer.Original);
            disagreements.AddRange(answers.Where(answer => matcher(answer.Command) != answer.Original)
                .Select(answer => $"{rule} (original {answer.Original}): {JsonSerializer.Serialize(answer.Command)}"));
        }

        output.WriteLine($"{corpus.Count} corpus strings, {commands.Count} distinct commands, {OriginalPatterns.Length} rules: "
            + $"{commands.Count * OriginalPatterns.Length} comparisons, {disagreements.Count} disagreements");
        Assert.True(corpus.Count > 1000, $"corpus of {corpus.Count}");
        Assert.True(disagreements.Count == 0, string.Join('\n', disagreements.Take(20)));
    }

    // Every string an [InlineData] row of this suite passes, and every GS-0N retro command.
    private static IEnumerable<string> Corpus() =>
        typeof(DangerousPatternOracleTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<InlineDataAttribute>().SelectMany(data => data.GetData(method)))
            .Where(row => row is not null)
            .SelectMany(row => row.OfType<string>())
            .Concat(typeof(GuardRetroCommands).GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!));

    // Each command after each lead, with four endings in turn.
    private static IEnumerable<string> Shaped() =>
        from lead in Leads.Select((text, index) => (text, index))
        from command in Commands.Select((text, index) => (text, index))
        from turn in Enumerable.Range(0, 4)
        select lead.text + command.text + Endings[(lead.index + command.index * 3 + turn * 5) % Endings.Length];

    // Words of the commands and leads, joined at random by separators, quotes and spaces.
    private static IEnumerable<string> TokenSoup()
    {
        var random = new Random(610);
        var words = Commands.Concat(Leads).SelectMany(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray();
        for (var i = 0; i < 8000; i++)
            yield return string.Concat(Enumerable.Range(0, random.Next(1, 9)).Select(_ => words[random.Next(words.Length)] + Joiners[random.Next(Joiners.Length)]));
    }

    private static IEnumerable<string> CharacterSoup()
    {
        var random = new Random(4610);
        for (var i = 0; i < 4000; i++)
            yield return new string(Enumerable.Range(0, random.Next(1, 25)).Select(_ => Alphabet[random.Next(Alphabet.Length)]).ToArray());
    }
}
