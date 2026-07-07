namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text;
using DynaDocs.Services;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Utils;

/// <summary>
/// The <c>dydo notion</c> command group (Decision 025 / 027). <c>connect</c> stores the show-once
/// integration token in the local secret store and records the storage policy in dydo.json;
/// <c>reveal-token</c> is a guarded break-glass that prints it back; <c>sync</c> resolves the token
/// (local store → namespaced env → generic env) and reconciles the project's sync model against Notion.
/// The token is never passed as a CLI argument, never echoed, and never logged.
/// </summary>
public static class NotionCommand
{
    public const string TokenEnvVar = NotionTokenResolver.TokenEnvVar;

    public static Command Create()
    {
        var command = new Command("notion", "Sync dydo docs with a Notion workspace");
        command.Subcommands.Add(CreateConnectCommand());
        command.Subcommands.Add(CreateRevealCommand());
        command.Subcommands.Add(CreateSyncCommand());
        return command;
    }

    private static Command CreateConnectCommand()
    {
        var command = new Command("connect", "Store a Notion integration token (read from stdin, never echoed)");
        var parentPage = new Option<string?>("--parent-page")
        {
            Description = "Notion page id the spine databases are provisioned under; written to dydo.json.",
        };
        var vault = new Option<bool>("--vault")
        {
            Description = "Seal the token into a committed, passphrase-encrypted vault instead of the local-only store.",
        };
        command.Options.Add(parentPage);
        command.Options.Add(vault);
        command.SetAction(parse => NotionConnectService.Execute(
            new ConfigService(),
            () => ReadSecretFromStdin("Paste the Notion token (input hidden): "),
            () => ConfirmYesNo(parse.GetValue(vault)
                ? "A vault already exists. Re-encrypt (rotate) it? [y/N] "
                : "A token is already stored. Overwrite it? [y/N] "),
            parse.GetValue(parentPage),
            parse.GetValue(vault),
            () => ReadSecretFromStdin("Vault passphrase (input hidden, entered twice): "),
            Console.Out,
            Console.Error));
        return command;
    }

    private static Command CreateRevealCommand()
    {
        var command = new Command("reveal-token", "Print the stored Notion token (guarded — exposes a secret)");
        var yes = new Option<bool>("--yes")
        {
            Description = "Skip the interactive confirmation.",
        };
        command.Options.Add(yes);
        command.SetAction(parse => NotionRevealService.Execute(
            new ConfigService(),
            parse.GetValue(yes),
            () => ConfirmYesNo("Print the token to this terminal? [y/N] "),
            () => ReadSecretFromStdin("Vault passphrase (input hidden): "),
            Console.Out,
            Console.Error));
        return command;
    }

    private static Command CreateSyncCommand()
    {
        var command = new Command("sync", "Reconcile dydo docs against the configured Notion workspace");
        var dryRun = new Option<bool>("--dry-run")
        {
            Description = "Compute and print the reconcile plan without applying any change.",
        };
        var prune = new Option<bool>("--prune")
        {
            Description = "Delete schema drift: properties or select options present in Notion but absent from the project's sync model. Without it, drift is warned about and left untouched.",
        };
        var parentPage = new Option<string?>("--parent-page")
        {
            Description = "Notion page id to mirror under, overriding notion.parentPageId / DYDO_NOTION_PARENT_PAGE. Point it at a scratch page to smoke-test without touching the configured workspace.",
        };
        var docs = new Option<bool>("--docs")
        {
            Description = "Also run the docs nested-page mirror alongside the PM spine. Off by default — the plain sync runs the spine only.",
        };
        var docsOnly = new Option<bool>("--docs-only")
        {
            Description = "Run only the docs nested-page mirror, skipping the PM spine. Mutually exclusive with --spine-only.",
        };
        var spineOnly = new Option<bool>("--spine-only")
        {
            Description = "Run only the PM spine, skipping the docs mirror (the default scope). Mutually exclusive with --docs-only.",
        };
        command.Options.Add(dryRun);
        command.Options.Add(prune);
        command.Options.Add(parentPage);
        command.Options.Add(docs);
        command.Options.Add(docsOnly);
        command.Options.Add(spineOnly);
        command.SetAction(parse => RunSync(
            parse.GetValue(dryRun), parse.GetValue(prune),
            parse.GetValue(parentPage), parse.GetValue(docs), parse.GetValue(docsOnly), parse.GetValue(spineOnly)));
        return command;
    }

    private static int RunSync(bool dryRun, bool prune, string? parentPageOverride, bool docs, bool docsOnly, bool spineOnly)
    {
        if (docsOnly && spineOnly)
        {
            Console.Error.WriteLine("notion sync: --docs-only and --spine-only are mutually exclusive.");
            return ExitCodes.ValidationErrors;
        }

        // --docs asks to ADD the mirror; --spine-only asks to SKIP it — contradictory. Reject rather than
        // silently letting --spine-only win and dropping --docs with no word (issue 0221).
        if (docs && spineOnly)
        {
            Console.Error.WriteLine("notion sync: --docs and --spine-only are mutually exclusive.");
            return ExitCodes.ValidationErrors;
        }

        var config = new ConfigService();
        var loaded = config.LoadConfig();
        var token = NotionTokenResolver.Resolve(
            loaded, config.GetProjectRoot(), config.GetDydoRoot(),
            () => ReadSecretFromStdin("Vault passphrase (input hidden): "));

        // Fail CLOSED for vault mode: a project that has opted into a committed vault but can't unlock it
        // (wrong/rotted passphrase, or no local key on a fresh clone) must not silently no-op — that would
        // leave CI green while sync stops, and the generic "not configured" hint points at the wrong knob.
        // A missing vault file still falls through to the clean no-op inside NotionSyncService.
        if (token == null
            && (loaded?.Notion?.TokenStorage ?? NotionTokenStore.LocalMode) == NotionTokenStore.VaultMode
            && File.Exists(NotionTokenStore.VaultPathFor(config.GetDydoRoot())))
        {
            Console.Error.WriteLine(
                "notion sync: could not unlock the vault (no local key or wrong passphrase). Run `dydo notion connect --vault`, or set the passphrase env var for CI.");
            return ExitCodes.ToolError;
        }

        return NotionSyncService.Execute(
            token, config, CreateClient, dryRun, Console.Out, Console.Error, prune, parentPageOverride, docs, docsOnly, spineOnly);
    }

    /// <summary>Reads a secret (token or passphrase) from stdin: masked when a TTY (so it never lands in
    /// shell history or the terminal), a plain line read when input is redirected (piped/tested).</summary>
    // Excluded from coverage: the masked branch drives Console.ReadKey, which throws under a test host
    // (no interactive console); it cannot be exercised without a real TTY. The redirected branch is a
    // trivial ReadLine covered indirectly by the connect command tests.
    [ExcludeFromCodeCoverage(Justification = "Interactive TTY input (Console.ReadKey) is not runnable under a test host.")]
    private static string? ReadSecretFromStdin(string prompt)
    {
        if (Console.IsInputRedirected)
            return Console.In.ReadLine();

        Console.Error.Write(prompt);
        var secret = new StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                    secret.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
            }
        }

        Console.Error.WriteLine();
        return secret.ToString();
    }

    private static bool ConfirmYesNo(string prompt)
    {
        Console.Error.Write(prompt);
        return IsAffirmative(Console.In.ReadLine());
    }

    /// <summary>Pure yes/no parse, extracted so the confirmation branch is unit-testable without a console:
    /// only a trimmed, case-insensitive <c>"y"</c> affirms; everything else — including <c>null</c> and
    /// <c>"yes"</c> — declines (fail-safe for the overwrite/reveal guards).</summary>
    internal static bool IsAffirmative(string? line) =>
        line != null && line.Trim().Equals("y", StringComparison.OrdinalIgnoreCase);

    /// <summary>The real transport: a fresh <see cref="HttpClient"/> wrapped by <see cref="NotionClient"/>.
    /// The handler is owned by the client for the process lifetime of one sync invocation.</summary>
    private static INotionClient CreateClient(string token) => new NotionClient(new HttpClient(), token);
}
