namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Utils;

/// <summary>The sync daemon's cheap delta tick (ns-13): a quiet tick reads only its boundary pages (0-2, never the
/// corpus) at any board size, a stamp-changed page gets its body re-read, a same-minute re-edit still reconciles
/// (F1), the daemon's own push does not churn (F6), local deletions propagate every tick, and the periodic census
/// surfaces a remote archive the fast ticks skipped — all without paying O(corpus). The manual sync seeds the
/// cheap-tick state (F2c), so the first daemon tick after it is already warm.</summary>
[Collection("ConsoleOutput")]
public sealed class NotionSpineDeltaTests : IDisposable
{
    private readonly string _dir;

    public NotionSpineDeltaTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-delta-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void QuietTick_ReadsOnlyBoundaryPages_AndOneFilteredQuery()
    {
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm tick (state seeded by the manual sync)

            var bodyBefore = client.GetBlockChildrenCalls;
            var sinceBefore = client.QueryDataSourceSinceCalls;
            var result = DeltaTick(client, census: false);

            Assert.True(result.Quiet);
            Assert.True(client.GetBlockChildrenCalls - bodyBefore <= 2);      // only boundary pages, never the corpus
            Assert.Equal(sinceBefore + 1, client.QueryDataSourceSinceCalls);  // exactly one filtered query for the type
        });
    }

    [Fact]
    public void ScaleInvariance_QuietTickRequestCount_IsConstantInCorpusSize()
    {
        var small = MeasureQuietTick(50);
        var large = MeasureQuietTick(5000);

        Assert.Equal(small, large);      // identical request profile at 50 and 5,000 records
        Assert.Equal(1, small.Since);    // one filtered query
        Assert.Equal(0, small.Full);     // zero full sweeps
        Assert.True(small.Body <= 2);    // only the boundary page(s)
    }

    [Fact]
    public void EditedBoundaryPage_IsRereadAndWrittenToRepo()
    {
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm

            // The newest page is the cursor boundary; a genuine later edit to it is read and written back.
            var pageId = BoundaryPageId(client);
            client.SetBlockChildren(pageId, [Paragraph("edited remotely")]); // F6 bumps its stamp past the cursor
            var localId = LocalIdOf(client, pageId);

            var bodyBefore = client.GetBlockChildrenCalls;
            var result = DeltaTick(client, census: false);

            Assert.Equal(bodyBefore + 1, client.GetBlockChildrenCalls); // exactly the one page
            Assert.True(result.Updated >= 1);
            Assert.Contains("edited remotely", File.ReadAllText(Path.Combine(project, "dydo", "project", "notes", localId + ".md")));
        });
    }

    [Fact]
    public void SameMinuteReedit_OfABoundaryPage_StillReconciles_NotDedupedForever()
    {
        // F1 regression: an edit landing in the SAME minute as the cursor (so the stamp does not change) must not be
        // deduped away — boundary pages are re-read every tick, so the change is seen.
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm

            var pageId = BoundaryPageId(client);
            var localId = LocalIdOf(client, pageId);
            client.PinnedStamp = client.QueryDataSource("ds-1").First(p => p.Id == pageId).LastEditedTime; // pin = cursor
            client.SetBlockChildren(pageId, [Paragraph("same minute edit")]); // stamp stays == cursor

            var result = DeltaTick(client, census: false);

            Assert.True(result.Updated >= 1);
            Assert.Contains("same minute edit", File.ReadAllText(Path.Combine(project, "dydo", "project", "notes", localId + ".md")));
        });
    }

    [Fact]
    public void DaemonPush_DoesNotChurn_TheEchoReconcilesNone()
    {
        // F6 push-echo: a local edit is pushed to the board (bumping the page's stamp), so next tick the page is a
        // boundary hit — but its board body now equals the base, so the reconcile is None. No second push, no loop.
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm

            var notePath = Path.Combine(project, "dydo", "project", "notes", "n00.md");
            File.WriteAllText(notePath, "---\ntitle: N0\nstatus: open\n---\n\nLocally edited body.");
            var pushTick = DeltaTick(client, census: false);
            Assert.True(pushTick.Updated >= 1); // the local edit was pushed

            var echoTick = DeltaTick(client, census: false);
            Assert.Equal(0, echoTick.Updated); // the daemon's own push does not re-push
            Assert.True(echoTick.Quiet);
        });
    }

    [Fact]
    public void LocalDeletion_DetectedOnAFastTick_ArchivesRemote_WithoutReadingItsBody()
    {
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm

            var pageId = ExternalIdOf(client, "n00");
            File.Delete(Path.Combine(project, "dydo", "project", "notes", "n00.md"));

            var bodyBefore = client.GetBlockChildrenCalls;
            var result = DeltaTick(client, census: false);

            Assert.True(result.Archived >= 1);
            Assert.True(client.IsArchived(pageId));                       // the local delete propagated to Notion
            Assert.True(client.GetBlockChildrenCalls - bodyBefore <= 1);  // n00 uses the base; only a boundary read at most
        });
    }

    [Fact]
    public void Census_DetectsRemoteArchiveFastTicksSkip_WithoutReadingItsBody()
    {
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm

            var pageId = ExternalIdOf(client, "n00");
            client.QueryDataSource("ds-1").First(p => p.Id == pageId).Archived = true; // archived in Notion

            // A fast tick cannot see the archive (a filtered query never returns archived pages).
            var fast = DeltaTick(client, census: false);
            Assert.Equal(0, fast.Archived);
            Assert.True(File.Exists(Path.Combine(project, "dydo", "project", "notes", "n00.md")));

            // The census does — reading only boundary bodies to find it, never the archived page's.
            var bodyBeforeCensus = client.GetBlockChildrenCalls;
            DeltaTick(client, census: true);
            Assert.True(client.GetBlockChildrenCalls - bodyBeforeCensus <= 1);
            Assert.False(File.Exists(Path.Combine(project, "dydo", "project", "notes", "n00.md"))); // archive propagated
        });
    }

    [Fact]
    public void Census_MassRemoteArchive_TripsFuse_WithoutDeletingFiles_OrAdvancingCursor()
    {
        WithProject(count: 10, (project, client) =>
        {
            DeltaTick(client, census: false); // warm
            var stateBefore = File.ReadAllText(FirstTypeDeltaPath(project));

            // A Notion-side sweep archives 6 of 10 pages — more than the fuse tolerates.
            foreach (var page in client.QueryDataSource("ds-1").Take(6))
                page.Archived = true;

            var result = DeltaTick(client, census: true);

            Assert.True(result.FuseTrips >= 1);
            // The fuse held: every note file is still on disk (nothing was deleted)...
            Assert.Equal(10, Directory.GetFiles(Path.Combine(project, "dydo", "project", "notes")).Length);
            // ...and the cheap-tick state was NOT advanced (F4), so the next tick re-detects the same edits.
            Assert.Equal(stateBefore, File.ReadAllText(FirstTypeDeltaPath(project)));
        });
    }

    [Fact]
    public void ValidateProvisioningTick_ReprobesDatabases_AndStaysQuiet()
    {
        WithProject(count: 2, (project, client) =>
        {
            DeltaTick(client, census: false); // warm
            var result = NotionSyncService.DeltaTick("tok", new ConfigService(), _ => client, census: false, validateProvisioning: true);
            Assert.True(result.Quiet);
        });
    }

    [Fact]
    public void Census_NoRemoteChange_IsQuiet()
    {
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm
            var result = DeltaTick(client, census: true);
            Assert.True(result.Quiet);
            Assert.True(result.Census);
        });
    }

    [Fact]
    public void RemoteCreatedPage_IsPulledIntoTheRepo()
    {
        WithProject(count: 2, (project, client) =>
        {
            DeltaTick(client, census: false); // warm

            var props = new Dictionary<string, NotionPropertyValue>
            {
                ["title"] = new() { Type = "title", Title = NotionRichText.Of("Remote") },
            };
            client.SeedPage("remotepage", props, [Paragraph("made in notion")], dataSourceId: "ds-1");
            client.SetLastEditedTime("remotepage", "2027-01-01T00:00:00.000Z"); // newer than the cursor -> a filter hit

            var result = DeltaTick(client, census: false);

            Assert.True(result.Created >= 1);
            Assert.True(File.Exists(Path.Combine(project, "dydo", "project", "notes", "remotepage.md")));
        });
    }

    [Fact]
    public void ResolvedShadow_IsPromotedOnADeltaTick()
    {
        // ns-13 F3: a shadow a human has resolved (no markers) is promoted at the start of a delta tick — its content
        // becomes the canonical doc and the shadow is consumed, rather than lingering to be re-conflicted.
        WithProject(count: 1, (project, client) =>
        {
            DeltaTick(client, census: false); // warm

            var shadow = Path.Combine(project, "dydo", "_system", "notion_sync_spine", "Note", "n00.md");
            Directory.CreateDirectory(Path.GetDirectoryName(shadow)!);
            File.WriteAllText(shadow, "---\ntitle: N0\nstatus: done\n---\n\nhuman resolved body");

            DeltaTick(client, census: false);

            Assert.False(File.Exists(shadow)); // consumed by promotion
            Assert.Contains("human resolved body", File.ReadAllText(Path.Combine(project, "dydo", "project", "notes", "n00.md")));
        });
    }

    [Fact]
    public void ResolvedShadow_ForAFolderRoutedDoc_PromotesIntoItsSubfolder_NoDuplicateStem()
    {
        // ns-13 F9: a resolved shadow for a doc the model routes into a subfolder must promote back to that ROUTED
        // path, not the type-dir root — else a duplicate stem wedges the tick (File.Move onto an existing file) and
        // hard-errors the next manual sync on IndexByLocalId.
        WithModel(RoutedModel,
            notes =>
            {
                Directory.CreateDirectory(Path.Combine(notes, "resolved"));
                File.WriteAllText(Path.Combine(notes, "resolved", "n00.md"), "---\ntitle: N0\nstatus: resolved\n---\n\nOriginal.");
                File.WriteAllText(Path.Combine(notes, "n01.md"), "---\ntitle: N1\nstatus: open\n---\n\nOther.");
            },
            (project, client) =>
            {
                DeltaTick(client, census: false); // warm

                var notes = Path.Combine(project, "dydo", "project", "notes");
                var shadow = Path.Combine(project, "dydo", "_system", "notion_sync_spine", "Note", "n00.md");
                Directory.CreateDirectory(Path.GetDirectoryName(shadow)!);
                File.WriteAllText(shadow, "---\ntitle: N0\nstatus: resolved\n---\n\nhuman resolved body");

                var result = DeltaTick(client, census: false); // must be clean — no wedge

                Assert.Equal(0, result.FuseTrips);
                Assert.False(File.Exists(shadow));                                   // consumed
                Assert.False(File.Exists(Path.Combine(notes, "n00.md")));            // NOT duplicated at the root
                Assert.Contains("human resolved body", File.ReadAllText(Path.Combine(notes, "resolved", "n00.md")));
            });
    }

    [Fact]
    public void IdleTick_LeavesDeltaStateByteIdentical()
    {
        // minor 1: in steady state the newest page is always a boundary hit that reconciles to None — an idle tick
        // must NOT rewrite delta.json (no multi-MB write every 15s at 100x).
        WithProject(count: 3, (project, client) =>
        {
            DeltaTick(client, census: false); // warm
            var before = File.ReadAllText(FirstTypeDeltaPath(project));

            var result = DeltaTick(client, census: false);

            Assert.True(result.Quiet);
            Assert.Equal(before, File.ReadAllText(FirstTypeDeltaPath(project)));
        });
    }

    [Fact]
    public void ManualSync_SeedsWarmState_SoTheFirstDaemonTickIsCheap()
    {
        // F2c: after a manual full sync the delta state exists (non-null cursor + file mtimes), so the daemon's very
        // first tick is a normal filtered tick — no O(corpus) cold-start read, no remote gap.
        WithProject(count: 3, (project, client) =>
        {
            var deltaPath = FirstTypeDeltaPath(project);
            Assert.True(File.Exists(deltaPath));
            Assert.Contains("cursor", File.ReadAllText(deltaPath));

            var before = client.GetBlockChildrenCalls;
            var result = DeltaTick(client, census: false); // the FIRST daemon tick
            Assert.True(result.Quiet);
            Assert.True(client.GetBlockChildrenCalls - before <= 2); // boundary only, not the whole corpus
        });
    }

    [Fact]
    public void DegradedColdStart_MissingState_ReconcilesLocalChanges_AndReestablishesCursor()
    {
        // F2a/b: if the cheap-tick state is lost/corrupt, a tick degrades to correctness — it reconciles the local
        // changes it can see (never discards them) and re-establishes a non-null cursor, without reading remote bodies.
        WithProject(count: 2, (project, client) =>
        {
            File.Delete(FirstTypeDeltaPath(project)); // simulate lost/corrupt state -> next tick has a null cursor
            File.WriteAllText(Path.Combine(project, "dydo", "project", "notes", "n00.md"),
                "---\ntitle: N0\nstatus: done\n---\n\nedited while state was gone");

            var bodyBefore = client.GetBlockChildrenCalls;
            var result = DeltaTick(client, census: false);

            Assert.True(result.Updated >= 1);                              // the local edit still synced (F2a)
            // No O(corpus) remote body storm: a cold start reads no bodies to DISCOVER changes; the only read is the
            // push's own body-replace of the single edited page.
            Assert.True(client.GetBlockChildrenCalls - bodyBefore <= 1);
            Assert.Contains("cursor", File.ReadAllText(FirstTypeDeltaPath(project))); // baseline re-established
        });
    }

    [Fact]
    public void ColdStart_EmptyBoard_SetsSentinelCursor_NotAnotherColdStart()
    {
        // F2b: a cold start over an empty (all-archived) board has no stamp to seed from — it must set the sentinel
        // epoch so the NEXT tick is a normal filtered tick, not another full cold-start read forever.
        WithProject(count: 1, (project, client) =>
        {
            foreach (var page in client.QueryDataSource("ds-1")) page.Archived = true;
            File.Delete(FirstTypeDeltaPath(project));

            DeltaTick(client, census: false);

            Assert.Contains(NotionDeltaState.SentinelEpoch, File.ReadAllText(FirstTypeDeltaPath(project)));
        });
    }

    [Fact]
    public void DaemonConfigError_ReportsMissingToken_NoProject_AndNullWhenReady()
    {
        Assert.Contains("not configured", NotionSyncService.DaemonConfigError(null, new ConfigService()));

        var savedCwd = Directory.GetCurrentDirectory();
        var bare = Path.Combine(_dir, "bare");
        Directory.CreateDirectory(bare);
        try
        {
            Directory.SetCurrentDirectory(bare);
            Assert.Contains("no dydo.json", NotionSyncService.DaemonConfigError("tok", new ConfigService()));
        }
        finally { Directory.SetCurrentDirectory(savedCwd); }

        WithProject(count: 1, (_, _) => Assert.Null(NotionSyncService.DaemonConfigError("tok", new ConfigService())));
    }

    /// <summary>Run a quiet tick against a fresh corpus of <paramref name="count"/> records and return the request
    /// deltas it issued: (filtered queries, full sweeps, body reads).</summary>
    private (int Since, int Full, int Body) MeasureQuietTick(int count)
    {
        (int, int, int) measured = default;
        WithProject(count, (_, client) =>
        {
            DeltaTick(client, census: false); // warm
            var s = client.QueryDataSourceSinceCalls;
            var f = client.QueryDataSourceCalls;
            var b = client.GetBlockChildrenCalls;
            DeltaTick(client, census: false); // the measured quiet tick
            measured = (client.QueryDataSourceSinceCalls - s, client.QueryDataSourceCalls - f, client.GetBlockChildrenCalls - b);
        });
        return measured;
    }

    private const string FlatModel = """
        { "objects": [ { "type": "Note", "dir": "project/notes", "notionTitle": "Notes",
            "properties": { "title": { "type": "title" },
              "status": { "type": "select", "options": ["open", "done"] } } } ] }
        """;

    // A model that ROUTES status "resolved" into a resolved/ subfolder — the live shape (issue resolved→resolved/).
    private const string RoutedModel = """
        { "objects": [ { "type": "Note", "dir": "project/notes", "notionTitle": "Notes",
            "properties": { "title": { "type": "title" },
              "status": { "type": "select", "options": ["open", "resolved"], "folders": { "resolved": "resolved" } } } } ] }
        """;

    /// <summary>Stand up a one-type project with the flat model + <paramref name="count"/> open docs.</summary>
    private void WithProject(int count, Action<string, FakeNotionClient> body) =>
        WithModel(FlatModel, notes =>
        {
            for (var i = 0; i < count; i++)
                File.WriteAllText(Path.Combine(notes, $"n{i:D2}.md"), $"---\ntitle: N{i}\nstatus: open\n---\n\nBody {i}.");
        }, body);

    /// <summary>Stand up a project with the given model, seed its docs, run a full manual sync (provision + create +
    /// base + seed the delta state), then hand the caller the project root and the page-holding fake client.</summary>
    private void WithModel(string modelJson, Action<string> seedDocs, Action<string, FakeNotionClient> body)
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = Path.Combine(_dir, "proj-" + Guid.NewGuid().ToString("N")[..6]);
        var notes = Path.Combine(project, "dydo", "project", "notes");
        Directory.CreateDirectory(notes);
        File.WriteAllText(Path.Combine(project, "dydo.json"), "{\"version\":1,\"notion\":{\"parentPageId\":\"page-root\"}}");
        var model = Path.Combine(project, "dydo", "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(model)!);
        File.WriteAllText(model, modelJson);
        seedDocs(notes);
        var client = new FakeNotionClient();
        try
        {
            Directory.SetCurrentDirectory(project);
            NotionSyncService.Execute("tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), new StringWriter());
            body(project, client);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    private static NotionDeltaTickResult DeltaTick(FakeNotionClient client, bool census) =>
        NotionSyncService.DeltaTick("tok", new ConfigService(), _ => client, census, validateProvisioning: false);

    private static string ExternalIdOf(FakeNotionClient client, string localId) =>
        client.QueryDataSource("ds-1").First(p =>
            NotionRichText.Flatten(p.Properties.Values.FirstOrDefault(v => v.Type == "title")?.Title) == "N" + int.Parse(localId[1..])).Id;

    private static string LocalIdOf(FakeNotionClient client, string pageId)
    {
        var title = NotionRichText.Flatten(client.QueryDataSource("ds-1").First(p => p.Id == pageId).Properties.Values.First(v => v.Type == "title").Title);
        return "n" + int.Parse(title[1..]).ToString("D2");
    }

    private static string BoundaryPageId(FakeNotionClient client) =>
        client.QueryDataSource("ds-1").OrderByDescending(p => p.LastEditedTime, StringComparer.Ordinal).First().Id;

    private static string FirstTypeDeltaPath(string project)
    {
        var syncDir = Path.Combine(project, "dydo", "_system", ".local", "sync");
        return Directory.EnumerateFiles(syncDir, "delta.json", SearchOption.AllDirectories).First();
    }

    private static NotionBlock Paragraph(string text) => new()
    {
        Type = "paragraph",
        Paragraph = new NotionBlockBody { RichText = NotionRichText.Of(text) },
    };
}
