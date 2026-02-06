namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Audit command for viewing and visualizing agent activity logs.
/// </summary>
public static class AuditCommand
{
    // Agent color palette for multi-agent visualization
    private static readonly string[] AgentColors =
    [
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4",
        "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F"
    ];

    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path")
        {
            DefaultValueFactory = _ => null,
            Description = "Path filter (e.g., /2025 for year 2025)"
        };

        var listOption = new Option<bool>("--list")
        {
            Description = "List available sessions"
        };
        var sessionOption = new Option<string?>("--session")
        {
            Description = "Show details for a specific session ID"
        };

        var command = new Command("audit", "View and visualize agent activity logs");
        command.Arguments.Add(pathArgument);
        command.Options.Add(listOption);
        command.Options.Add(sessionOption);

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArgument);
            var list = parseResult.GetValue(listOption);
            var sessionId = parseResult.GetValue(sessionOption);

            if (list)
                return ExecuteList(path);
            if (!string.IsNullOrEmpty(sessionId))
                return ExecuteShowSession(sessionId);
            return ExecuteGenerateVisualization(path);
        });

        return command;
    }

    private static int ExecuteList(string? yearFilter)
    {
        try
        {
            var auditService = new AuditService();
            var files = auditService.ListSessionFiles(yearFilter?.TrimStart('/'));

            if (files.Count == 0)
            {
                Console.WriteLine("No audit sessions found.");
                return ExitCodes.Success;
            }

            Console.WriteLine($"Found {files.Count} session(s):");
            Console.WriteLine();

            foreach (var file in files.Take(50))
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                Console.WriteLine($"  {filename}");
            }

            if (files.Count > 50)
            {
                Console.WriteLine($"  ... and {files.Count - 50} more");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error listing sessions: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static int ExecuteShowSession(string sessionId)
    {
        try
        {
            var auditService = new AuditService();
            var session = auditService.GetSession(sessionId);

            if (session == null)
            {
                Console.WriteLine($"Session not found: {sessionId}");
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Session: {session.SessionId}");
            Console.WriteLine($"Agent: {session.AgentName ?? "(none)"}");
            Console.WriteLine($"Human: {session.Human ?? "(none)"}");
            Console.WriteLine($"Started: {session.Started:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Git HEAD: {session.GitHead ?? "(none)"}");
            Console.WriteLine($"Snapshot: {(session.Snapshot != null ? $"{session.Snapshot.Files.Count} files" : "(none)")}");
            Console.WriteLine($"Events: {session.Events.Count}");
            Console.WriteLine();

            foreach (var e in session.Events)
            {
                var details = e.EventType switch
                {
                    AuditEventType.Read or AuditEventType.Write or AuditEventType.Edit or AuditEventType.Delete
                        => e.Path,
                    AuditEventType.Bash => TruncateCommand(e.Command ?? ""),
                    AuditEventType.Role => $"{e.Role}" + (e.Task != null ? $" on {e.Task}" : ""),
                    AuditEventType.Claim or AuditEventType.Release => e.AgentName,
                    AuditEventType.Commit => $"{e.CommitHash} {TruncateCommand(e.CommitMessage ?? "")}",
                    AuditEventType.Blocked => $"{e.Path ?? e.Command} - {e.BlockReason}",
                    _ => ""
                };

                Console.WriteLine($"  {e.Timestamp:HH:mm:ss} {e.EventType,-8} {details}");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error showing session: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static int ExecuteGenerateVisualization(string? yearFilter)
    {
        try
        {
            var auditService = new AuditService();

            // Ensure audit folder exists
            auditService.EnsureAuditFolder();

            var (sessions, limitReached) = auditService.LoadSessions(yearFilter?.TrimStart('/'));

            if (limitReached)
            {
                Console.WriteLine("WARNING: More than 10,000 session files. Consider filtering by year (e.g., dydo audit /2025)");
            }

            if (sessions.Count == 0)
            {
                Console.WriteLine("No audit sessions found.");
                return ExitCodes.Success;
            }

            Console.WriteLine($"Loaded {sessions.Count} session(s).");

            // Generate HTML
            var html = GenerateVisualizationHtml(sessions);

            // Write to reports folder
            var reportsPath = Path.Combine(auditService.GetAuditPath(), "reports");
            Directory.CreateDirectory(reportsPath);

            var htmlPath = Path.Combine(reportsPath, "replay.html");
            File.WriteAllText(htmlPath, html);

            Console.WriteLine($"Generated: {htmlPath}");

            // Try to open in browser
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = htmlPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                Console.WriteLine("Opened in browser.");
            }
            catch
            {
                Console.WriteLine("Open the file in your browser to view.");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error generating visualization: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static string GenerateVisualizationHtml(IReadOnlyList<AuditSession> sessions)
    {
        // Assign colors to agents
        var agentColors = AssignAgentColors(sessions);

        // Merge all events across sessions, sorted by timestamp
        var mergedTimeline = MergeTimelines(sessions);

        // Build combined snapshot (union of all files/folders/links)
        var combinedSnapshot = BuildCombinedSnapshot(sessions);

        // Serialize data for embedding
        var sessionsJson = JsonSerializer.Serialize(sessions, DydoDefaultJsonContext.Default.ListAuditSession);
        var agentColorsJson = JsonSerializer.Serialize(agentColors, DydoDefaultJsonContext.Default.DictionaryStringString);
        var timelineJson = JsonSerializer.Serialize(mergedTimeline, DydoDefaultJsonContext.Default.ListMergedEvent);
        var snapshotJson = JsonSerializer.Serialize(combinedSnapshot, DydoDefaultJsonContext.Default.ProjectSnapshot);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <title>Audit Visualization - DynaDocs</title>");
        sb.AppendLine("    <script src=\"https://unpkg.com/vis-network/standalone/umd/vis-network.min.js\"></script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <h1>Project Audit Visualization</h1>");
        sb.AppendLine();

        // Controls + Event log (side by side)
        sb.AppendLine("    <div style=\"display:flex; gap:10px; align-items:flex-start\">");
        sb.AppendLine("    <div>");
        sb.AppendLine("    <table border=\"1\" cellpadding=\"5\">");
        sb.AppendLine("        <tr>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                <button onclick=\"stepFirst()\">&lt;&lt;</button>");
        sb.AppendLine("                <button onclick=\"stepBack()\">&lt;</button>");
        sb.AppendLine("                <button onclick=\"togglePlay()\" id=\"playBtn\">Play</button>");
        sb.AppendLine("                <button onclick=\"stepForward()\">&gt;</button>");
        sb.AppendLine("                <button onclick=\"stepLast()\">&gt;&gt;</button>");
        sb.AppendLine("                <button onclick=\"reset()\">Reset</button>");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                Speed: <input type=\"range\" id=\"speed\" min=\"50\" max=\"2000\" value=\"300\">");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                Step: <span id=\"stepNum\">0</span> / <span id=\"totalSteps\">0</span>");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                <input type=\"checkbox\" id=\"showDocLinks\" onchange=\"toggleDocLinks()\">");
        sb.AppendLine("                <label for=\"showDocLinks\">Show doc links</label>");
        sb.AppendLine("            </td>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine();

        // Agent legend
        sb.AppendLine("    <table border=\"1\" cellpadding=\"3\" style=\"margin-top:5px\">");
        sb.AppendLine("        <tr>");
        sb.AppendLine("            <td><b>Agents:</b></td>");
        sb.AppendLine("            <td id=\"agentLegend\"></td>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine();
        // Event log (right side, next to controls)
        sb.AppendLine("    <div style=\"flex:1; border:1px solid #ccc; padding:5px; min-width:300px\">");
        sb.AppendLine("        <b>Event Log</b>");
        sb.AppendLine("        <div id=\"eventLog\" style=\"height:200px; overflow-y:auto; font-family:monospace; font-size:11px; margin-top:5px\"></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine();

        // 3 Graph slots
        for (int i = 0; i < 3; i++)
        {
            var defaultEnabled = i < 2 ? "checked" : "";
            var defaultCollapsed = i >= 2 ? "none" : "block";
            sb.AppendLine($"    <div id=\"graphSlot{i}\" style=\"margin-top:10px; border:1px solid #ccc; padding:5px\">");
            sb.AppendLine($"        <b>Graph {i + 1}:</b>");
            sb.AppendLine($"        <select id=\"filter{i}\" onchange=\"onFilterChange({i})\"></select>");
            sb.AppendLine($"        <input type=\"checkbox\" id=\"enabled{i}\" onchange=\"onEnableChange({i})\" {defaultEnabled}>");
            sb.AppendLine($"        <label for=\"enabled{i}\">Enabled</label>");
            sb.AppendLine($"        <button onclick=\"toggleCollapse({i})\" id=\"collapseBtn{i}\">Collapse</button>");
            sb.AppendLine($"        <span id=\"nodeCount{i}\" style=\"margin-left:10px; color:#666\"></span>");
            sb.AppendLine($"        <div id=\"graphContainer{i}\" style=\"display:{defaultCollapsed}; margin-top:5px\">");
            sb.AppendLine($"            <div id=\"graph{i}\" style=\"width:95vw; height:80vh; border:1px solid #999\"></div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
        }
        sb.AppendLine();

        // Sessions info
        sb.AppendLine("    <h3>Sessions Loaded</h3>");
        sb.AppendLine("    <table border=\"1\" cellpadding=\"5\" id=\"sessionsTable\">");
        sb.AppendLine("        <tr><th>Agent</th><th>Human</th><th>Started</th><th>Events</th><th>Snapshot</th></tr>");
        foreach (var session in sessions)
        {
            var snapshotInfo = session.Snapshot != null
                ? $"{session.Snapshot.Files.Count} files"
                : "none";
            sb.AppendLine($"        <tr><td>{session.AgentName ?? "Unknown"}</td><td>{session.Human ?? "-"}</td><td>{session.Started:yyyy-MM-dd HH:mm}</td><td>{session.Events.Count}</td><td>{snapshotInfo}</td></tr>");
        }
        sb.AppendLine("    </table>");
        sb.AppendLine();

        // Embedded data and JavaScript
        sb.AppendLine("    <script>");
        sb.AppendLine($"        const sessionsData = {sessionsJson};");
        sb.AppendLine($"        const agentColors = {agentColorsJson};");
        sb.AppendLine($"        const mergedTimeline = {timelineJson};");
        sb.AppendLine($"        const combinedSnapshot = {snapshotJson};");
        sb.AppendLine();
        sb.AppendLine(GetJavaScript());
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static Dictionary<string, string> AssignAgentColors(IReadOnlyList<AuditSession> sessions)
    {
        var colors = new Dictionary<string, string>();
        var colorIndex = 0;

        foreach (var session in sessions)
        {
            var agent = session.AgentName ?? session.SessionId;
            if (!colors.ContainsKey(agent))
            {
                colors[agent] = AgentColors[colorIndex % AgentColors.Length];
                colorIndex++;
            }
        }

        return colors;
    }

    private static List<MergedEvent> MergeTimelines(IReadOnlyList<AuditSession> sessions)
    {
        var merged = new List<MergedEvent>();

        foreach (var session in sessions)
        {
            var agent = session.AgentName ?? session.SessionId;
            foreach (var evt in session.Events)
            {
                merged.Add(new MergedEvent
                {
                    Timestamp = evt.Timestamp,
                    Agent = agent,
                    EventType = evt.EventType.ToString(),
                    Path = evt.Path,
                    Command = evt.Command,
                    Role = evt.Role,
                    Task = evt.Task
                });
            }
        }

        return merged.OrderBy(e => e.Timestamp).ToList();
    }

    private static ProjectSnapshot BuildCombinedSnapshot(IReadOnlyList<AuditSession> sessions)
    {
        var combined = new ProjectSnapshot
        {
            GitCommit = sessions.FirstOrDefault(s => s.Snapshot != null)?.Snapshot?.GitCommit ?? "unknown"
        };

        var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allLinks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in sessions.Where(s => s.Snapshot != null))
        {
            foreach (var file in session.Snapshot!.Files)
                allFiles.Add(file);
            foreach (var folder in session.Snapshot.Folders)
                allFolders.Add(folder);
            foreach (var (source, targets) in session.Snapshot.DocLinks)
            {
                if (!allLinks.ContainsKey(source))
                    allLinks[source] = new List<string>();
                foreach (var target in targets)
                {
                    if (!allLinks[source].Contains(target, StringComparer.OrdinalIgnoreCase))
                        allLinks[source].Add(target);
                }
            }
        }

        combined.Files = allFiles.OrderBy(f => f).ToList();
        combined.Folders = allFolders.OrderBy(f => f).ToList();
        combined.DocLinks = allLinks;

        return combined;
    }

    private static string GetJavaScript() => """
        // Playback state
        let currentStep = 0;
        let playing = false;
        let playInterval = null;
        let showDocLinks = false;
        let accessedFiles = new Set();

        // 3 graph slots
        const graphs = [
            { network: null, nodes: null, edges: null, filter: '', enabled: true, collapsed: false, nodeIdMap: {}, nextNodeId: 1, docLinkEdgeIds: [] },
            { network: null, nodes: null, edges: null, filter: '', enabled: true, collapsed: false, nodeIdMap: {}, nextNodeId: 1, docLinkEdgeIds: [] },
            { network: null, nodes: null, edges: null, filter: '', enabled: false, collapsed: true, nodeIdMap: {}, nextNodeId: 1, docLinkEdgeIds: [] }
        ];

        // Folder options for dropdowns
        let folderOptions = [];

        // Build lookup set for path normalization (event paths may be absolute/Windows)
        const snapshotPathSet = new Set();
        for (const file of combinedSnapshot.files) {
            snapshotPathSet.add(file.toLowerCase());
        }
        for (const folder of combinedSnapshot.folders) {
            snapshotPathSet.add(folder.toLowerCase());
        }

        function normalizeEventPath(eventPath) {
            if (!eventPath) return eventPath;
            let lower = eventPath.replace(/\\/g, '/').toLowerCase();
            if (snapshotPathSet.has(lower)) return lower;
            const parts = lower.split('/');
            for (let i = 1; i < parts.length; i++) {
                const suffix = parts.slice(i).join('/');
                if (snapshotPathSet.has(suffix)) return suffix;
            }
            return lower;
        }

        document.addEventListener('DOMContentLoaded', function() {
            buildAgentLegend();
            buildFolderOptions();
            populateDropdowns();
            initializeAllGraphs();
            document.getElementById('totalSteps').textContent = mergedTimeline.length;
        });

        function buildAgentLegend() {
            const legend = document.getElementById('agentLegend');
            let html = '';
            for (const [agent, color] of Object.entries(agentColors)) {
                html += '<span style="display:inline-block;margin-right:10px">';
                html += '<span style="display:inline-block;width:12px;height:12px;background:' + color + ';margin-right:3px"></span>';
                html += agent + '</span>';
            }
            legend.innerHTML = html;
        }

        function buildFolderOptions() {
            // Get unique top-level folders
            const topFolders = new Set();
            for (const file of combinedSnapshot.files) {
                const parts = file.split('/');
                if (parts.length > 1) {
                    topFolders.add(parts[0]);
                }
            }
            folderOptions = ['(All files)', ...Array.from(topFolders).sort()];
        }

        function populateDropdowns() {
            for (let i = 0; i < 3; i++) {
                const select = document.getElementById('filter' + i);
                select.innerHTML = '';
                for (const opt of folderOptions) {
                    const option = document.createElement('option');
                    option.value = opt === '(All files)' ? '' : opt;
                    option.textContent = opt;
                    select.appendChild(option);
                }
                // Set default filters
                if (i === 0 && folderOptions.includes('dydo')) {
                    select.value = 'dydo';
                    graphs[i].filter = 'dydo';
                } else if (i === 1 && folderOptions.length > 2) {
                    // Second non-dydo folder
                    const secondFolder = folderOptions.find(f => f !== '(All files)' && f !== 'dydo');
                    if (secondFolder) {
                        select.value = secondFolder;
                        graphs[i].filter = secondFolder;
                    }
                }
            }
        }

        function initializeAllGraphs() {
            for (let i = 0; i < 3; i++) {
                if (graphs[i].enabled && !graphs[i].collapsed) {
                    initGraph(i);
                }
            }
        }

        function initGraph(index) {
            const g = graphs[index];
            g.nodes = new vis.DataSet();
            g.edges = new vis.DataSet();
            g.nodeIdMap = {};
            g.nextNodeId = 1;
            g.docLinkEdgeIds = [];

            buildFilteredTree(index);

            const container = document.getElementById('graph' + index);
            g.network = new vis.Network(container, { nodes: g.nodes, edges: g.edges }, {
                layout: {
                    improvedLayout: false
                },
                physics: {
                    enabled: true,
                    stabilization: {
                        enabled: true,
                        iterations: 150,
                        updateInterval: 25
                    },
                    barnesHut: {
                        gravitationalConstant: -3000,
                        springLength: 120,
                        springConstant: 0.04
                    }
                },
                nodes: {
                    shape: 'box',
                    font: { size: 10 },
                    margin: 5
                },
                edges: {
                    smooth: { type: 'cubicBezier' }
                }
            });

            // Freeze physics after stabilization
            g.network.on('stabilized', function() {
                g.network.setOptions({ physics: { enabled: false } });
            });

            updateNodeCount(index);
        }

        function destroyGraph(index) {
            const g = graphs[index];
            if (g.network) {
                g.network.destroy();
                g.network = null;
                g.nodes = null;
                g.edges = null;
                g.nodeIdMap = {};
                g.nextNodeId = 1;
                g.docLinkEdgeIds = [];
            }
            document.getElementById('nodeCount' + index).textContent = '';
        }

        function buildFilteredTree(index) {
            const g = graphs[index];
            const filter = g.filter.toLowerCase();

            // Filter folders
            const filteredFolders = filter
                ? combinedSnapshot.folders.filter(f => f.toLowerCase().startsWith(filter))
                : combinedSnapshot.folders;

            // Filter files
            const filteredFiles = filter
                ? combinedSnapshot.files.filter(f => f.toLowerCase().startsWith(filter))
                : combinedSnapshot.files;

            // Add folder nodes
            for (const folder of filteredFolders) {
                const parts = folder.split('/');
                const label = '[' + parts[parts.length - 1] + ']';
                const id = getNodeId(index, folder);

                g.nodes.add({
                    id: id,
                    label: label,
                    title: folder,
                    color: '#FFFACD',
                    shape: 'box'
                });

                // Add edge to parent folder (if parent is in filtered set)
                if (parts.length > 1) {
                    const parentPath = parts.slice(0, -1).join('/');
                    if (!filter || parentPath.toLowerCase().startsWith(filter)) {
                        const parentId = g.nodeIdMap[parentPath.toLowerCase()];
                        if (parentId) {
                            g.edges.add({
                                from: parentId,
                                to: id,
                                color: { color: '#CCCCCC' },
                                dashes: false,
                                arrows: ''
                            });
                        }
                    }
                }
            }

            // Add file nodes
            for (const file of filteredFiles) {
                const parts = file.split('/');
                const label = parts[parts.length - 1];
                const id = getNodeId(index, file);
                const folderPath = parts.slice(0, -1).join('/');

                // Check if already accessed
                const isAccessed = accessedFiles.has(file.toLowerCase());

                g.nodes.add({
                    id: id,
                    label: label,
                    title: file,
                    color: isAccessed ? '#E8E8E8' : '#F5F5F5',
                    shape: 'box'
                });

                // Add edge to parent folder
                if (folderPath) {
                    const parentId = g.nodeIdMap[folderPath.toLowerCase()];
                    if (parentId) {
                        g.edges.add({
                            from: parentId,
                            to: id,
                            color: { color: '#CCCCCC' },
                            dashes: false,
                            arrows: ''
                        });
                    }
                }
            }

            if (showDocLinks) {
                addDocLinksToGraph(index);
            }
        }

        function addDocLinksToGraph(index) {
            const g = graphs[index];
            for (const [source, targets] of Object.entries(combinedSnapshot.doc_links)) {
                const sourceId = g.nodeIdMap[source.toLowerCase()];
                if (!sourceId) continue;

                for (const target of targets) {
                    const targetId = g.nodeIdMap[target.toLowerCase()];
                    if (!targetId) continue;

                    const edgeId = g.edges.add({
                        from: sourceId,
                        to: targetId,
                        color: { color: '#4169E1' },
                        dashes: true,
                        arrows: 'to'
                    })[0];
                    g.docLinkEdgeIds.push(edgeId);
                }
            }
        }

        function removeDocLinksFromGraph(index) {
            const g = graphs[index];
            for (const edgeId of g.docLinkEdgeIds) {
                try { g.edges.remove(edgeId); } catch(e) {}
            }
            g.docLinkEdgeIds = [];
        }

        function getNodeId(index, path) {
            const g = graphs[index];
            const key = path.toLowerCase();
            if (!g.nodeIdMap[key]) {
                g.nodeIdMap[key] = g.nextNodeId++;
            }
            return g.nodeIdMap[key];
        }

        function updateNodeCount(index) {
            const g = graphs[index];
            const count = g.nodes ? g.nodes.length : 0;
            document.getElementById('nodeCount' + index).textContent = count + ' nodes';
        }

        function onFilterChange(index) {
            const g = graphs[index];
            g.filter = document.getElementById('filter' + index).value;
            if (g.enabled && !g.collapsed && g.network) {
                destroyGraph(index);
                initGraph(index);
                // Replay events to restore accessed state
                replayEventsToGraph(index);
            }
        }

        function onEnableChange(index) {
            const g = graphs[index];
            g.enabled = document.getElementById('enabled' + index).checked;
            if (g.enabled && !g.collapsed) {
                initGraph(index);
                replayEventsToGraph(index);
            } else {
                destroyGraph(index);
            }
        }

        function toggleCollapse(index) {
            const g = graphs[index];
            g.collapsed = !g.collapsed;
            const container = document.getElementById('graphContainer' + index);
            const btn = document.getElementById('collapseBtn' + index);

            if (g.collapsed) {
                container.style.display = 'none';
                btn.textContent = 'Expand';
                destroyGraph(index);
            } else {
                container.style.display = 'block';
                btn.textContent = 'Collapse';
                if (g.enabled) {
                    initGraph(index);
                    replayEventsToGraph(index);
                }
            }
        }

        function replayEventsToGraph(index) {
            // Flash nodes for already-processed events
            for (let i = 0; i < currentStep; i++) {
                const event = mergedTimeline[i];
                if (event.Path) {
                    const normalizedPath = normalizeEventPath(event.Path);
                    flashNodeInGraph(index, normalizedPath, event.EventType, false, event.Agent);
                }
            }
        }

        function processEvent(event) {
            const log = document.getElementById('eventLog');
            const agentColor = agentColors[event.Agent] || '#666666';
            const time = event.Timestamp.substring(11, 19);

            const line = document.createElement('div');
            line.innerHTML = '<span style="background:' + agentColor + ';color:white;padding:0 3px;margin-right:3px">' +
                event.Agent.substring(0, 8) + '</span>' +
                time + ' ' + event.EventType + ' ' +
                (event.Path || event.Command || event.Role || '');
            log.appendChild(line);
            log.scrollTop = log.scrollHeight;

            if (event.Path) {
                const normalizedPath = normalizeEventPath(event.Path);
                accessedFiles.add(normalizedPath);

                // Flash in all enabled graphs
                for (let i = 0; i < 3; i++) {
                    if (graphs[i].enabled && !graphs[i].collapsed && graphs[i].network) {
                        flashNodeInGraph(i, normalizedPath, event.EventType, true, event.Agent);
                    }
                }
            }
        }

        function flashNodeInGraph(index, path, eventType, animate, agentName) {
            const g = graphs[index];
            const pathLower = path.toLowerCase();

            // Check if path matches filter
            if (g.filter && !pathLower.startsWith(g.filter.toLowerCase())) {
                return;
            }

            const flashColor = agentColors[agentName] || '#CCCCCC';
            let nodeId = g.nodeIdMap[pathLower];

            if (!nodeId) {
                // Create node if not exists (for files not in snapshot)
                nodeId = getNodeId(index, path);
                const parts = path.split('/');
                g.nodes.add({
                    id: nodeId,
                    label: parts[parts.length - 1],
                    title: path,
                    color: flashColor,
                    shape: 'box'
                });
            } else if (animate) {
                // Flash the node with agent color + border highlight
                g.nodes.update({
                    id: nodeId,
                    color: { background: flashColor, border: '#333333' },
                    borderWidth: 3,
                    font: { size: 12, bold: true }
                });

                // Reset after delay
                setTimeout(() => {
                    g.nodes.update({
                        id: nodeId,
                        color: '#E8E8E8',
                        borderWidth: 1,
                        font: { size: 10, bold: false }
                    });
                }, 800);

                g.network.selectNodes([nodeId]);
            } else {
                // Just mark as accessed (no animation)
                g.nodes.update({ id: nodeId, color: '#E8E8E8' });
            }
        }

        function getEventColor(eventType) {
            switch(eventType) {
                case 'Read': return '#90EE90';
                case 'Write': return '#87CEEB';
                case 'Edit': return '#FFD700';
                case 'Delete': return '#FF6B6B';
                case 'Blocked': return '#FF0000';
                default: return '#CCCCCC';
            }
        }

        function toggleDocLinks() {
            showDocLinks = document.getElementById('showDocLinks').checked;
            for (let i = 0; i < 3; i++) {
                const g = graphs[i];
                if (g.enabled && !g.collapsed && g.network) {
                    if (showDocLinks) {
                        addDocLinksToGraph(i);
                    } else {
                        removeDocLinksFromGraph(i);
                    }
                }
            }
        }

        function rebuildAllGraphs() {
            for (let i = 0; i < 3; i++) {
                const g = graphs[i];
                if (g.enabled && !g.collapsed) {
                    destroyGraph(i);
                    initGraph(i);
                }
            }
        }

        function stepForward() {
            if (currentStep < mergedTimeline.length) {
                processEvent(mergedTimeline[currentStep]);
                currentStep++;
                updateStepDisplay();
            }
        }

        function stepBack() {
            if (currentStep > 0) {
                currentStep--;
                accessedFiles.clear();
                document.getElementById('eventLog').innerHTML = '';
                rebuildAllGraphs();
                // Replay events up to current step
                for (let i = 0; i < currentStep; i++) {
                    processEvent(mergedTimeline[i]);
                }
                updateStepDisplay();
            }
        }

        function stepFirst() {
            currentStep = 0;
            accessedFiles.clear();
            document.getElementById('eventLog').innerHTML = '';
            rebuildAllGraphs();
            updateStepDisplay();
        }

        function stepLast() {
            while (currentStep < mergedTimeline.length) {
                processEvent(mergedTimeline[currentStep]);
                currentStep++;
            }
            updateStepDisplay();
        }

        function togglePlay() {
            playing = !playing;
            document.getElementById('playBtn').textContent = playing ? 'Pause' : 'Play';

            if (playing) {
                const speed = 2100 - parseInt(document.getElementById('speed').value);
                playInterval = setInterval(function() {
                    if (currentStep >= mergedTimeline.length) {
                        togglePlay();
                    } else {
                        stepForward();
                    }
                }, speed);
            } else {
                clearInterval(playInterval);
            }
        }

        function reset() {
            if (playing) togglePlay();
            currentStep = 0;
            accessedFiles.clear();
            document.getElementById('eventLog').innerHTML = '';
            rebuildAllGraphs();
            updateStepDisplay();
        }

        function updateStepDisplay() {
            document.getElementById('stepNum').textContent = currentStep;
        }
""";

    private static string TruncateCommand(string command)
    {
        const int maxLength = 50;
        if (command.Length <= maxLength)
            return command;
        return command[..maxLength] + "...";
    }
}

/// <summary>
/// Represents a merged event from multiple sessions for timeline visualization.
/// Internal to allow source-generated JSON serialization.
/// </summary>
internal class MergedEvent
{
    public DateTime Timestamp { get; set; }
    public string Agent { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? Path { get; set; }
    public string? Command { get; set; }
    public string? Role { get; set; }
    public string? Task { get; set; }
}
