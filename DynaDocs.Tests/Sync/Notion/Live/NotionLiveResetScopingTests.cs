namespace DynaDocs.Tests.Sync.Notion.Live;

using System.Text;
using DynaDocs.Services;
using DynaDocs.Sync.Notion;
using DynaDocs.Utils;

/// <summary>
/// LIVE (ns-9, issue 0257): a real <c>dydo notion reset</c> scoped to the scratch parent archives ONLY the
/// scratch board and leaves a second (configured) parent's provision STATE byte-identical. The reverted 0257
/// attempt let a scratch reset poison the configured board's state; this drives the real <see cref="NotionReset"/>
/// against live Notion for the scratch parent while a decoy state file stands in for a second board (no second
/// live board is provisioned — "state files only"), and asserts that file is untouched.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveResetScopingTests : NotionLiveTestBase
{
    private const string ConfiguredSecondParent = "22222222222222222222222222222222";

    [NotionLiveFact]
    public void ScratchReset_LeavesConfiguredParentStateUntouched()
    {
        var (token, _) = NotionLiveEnv.RequireConfig();
        var savedCwd = Directory.GetCurrentDirectory();
        var project = Path.Combine(Path.GetTempPath(), "dydo-live-reset-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var dydoRoot = SetUpProject(project);
            Directory.SetCurrentDirectory(project);
            var config = new ConfigService();

            // Provision the SCRATCH board (under the live child page) so the reset has a real database to archive.
            Assert.Equal(ExitCodes.Success, NotionSyncService.Execute(
                token, config, _ => Client, dryRun: false, TextWriter.Null, TextWriter.Null, parentPageOverride: ChildPageId));

            // Stand up the configured (second) parent's scoped state file — the file a scratch reset must never touch.
            var decoyPath = NotionSpineState.Resolve(dydoRoot, ConfiguredSecondParent, null, dryRun: true, TextWriter.Null).ProvisionPath;
            Directory.CreateDirectory(Path.GetDirectoryName(decoyPath)!);
            var sentinel = Encoding.UTF8.GetBytes("{\"types\":[{\"objectType\":\"Decoy\",\"databaseId\":\"decoy-db\",\"dataSourceId\":\"decoy-ds\"}]}");
            File.WriteAllBytes(decoyPath, sentinel);

            var resetOutput = new StringWriter();
            Assert.Equal(ExitCodes.Success, NotionReset.Execute(
                token, config, _ => Client, dryRun: false, confirm: () => true,
                resetOutput, TextWriter.Null, parentPageOverride: ChildPageId));

            // The reset actually DID something to the scratch board (a no-op reset would pass the negative assert
            // below vacuously): it archived at least the scratch Note database.
            Assert.Contains("archived", resetOutput.ToString());

            // The configured board's state survives the scratch reset byte-for-byte (0257 scoping).
            Assert.True(File.Exists(decoyPath));
            Assert.Equal(sentinel, File.ReadAllBytes(decoyPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            if (Directory.Exists(project)) Directory.Delete(project, recursive: true);
        }
    }

    /// <summary>A minimal dydo project whose configured parent is a fake second board and whose model is one
    /// relation-free type, so the scratch provision is a single database create with no post-pass. Returns the
    /// dydo root.</summary>
    private static string SetUpProject(string project)
    {
        var dydoRoot = Path.Combine(project, "dydo");
        Directory.CreateDirectory(Path.Combine(dydoRoot, "project", "notes"));
        File.WriteAllText(Path.Combine(project, "dydo.json"),
            $"{{\"version\":1,\"notion\":{{\"parentPageId\":\"{ConfiguredSecondParent}\"}}}}");

        var modelPath = Path.Combine(dydoRoot, "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        File.WriteAllText(modelPath, $$"""
            {
              "objects": [
                { "type": "Note", "dir": "project/notes", "notionTitle": "smoke-notes-{{Guid.NewGuid().ToString("N")[..6]}}",
                  "properties": { "title": { "type": "title" }, "status": { "type": "select", "options": ["a", "b"] } } }
              ]
            }
            """);
        return dydoRoot;
    }
}
