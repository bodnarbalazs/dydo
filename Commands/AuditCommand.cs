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

        // Controls row
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
        sb.AppendLine("                <input type=\"checkbox\" id=\"showAll\" onchange=\"toggleShowAll()\" checked>");
        sb.AppendLine("                <label for=\"showAll\">Show all files</label>");
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
        sb.AppendLine();

        // Main graph + event log
        sb.AppendLine("    <table border=\"1\" cellpadding=\"5\" style=\"margin-top:10px\">");
        sb.AppendLine("        <tr>");
        sb.AppendLine("            <td style=\"width:70%; height:600px; vertical-align:top\">");
        sb.AppendLine("                <div id=\"graph\" style=\"width:100%; height:100%\"></div>");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td style=\"width:30%; vertical-align:top\">");
        sb.AppendLine("                <div id=\"eventLog\" style=\"height:600px; overflow-y:auto; font-family:monospace; font-size:11px\"></div>");
        sb.AppendLine("            </td>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("    </table>");
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
        let currentStep = 0;
        let playing = false;
        let playInterval = null;
        let network = null;
        let nodes = null;
        let edges = null;
        let showAllFiles = true;
        let showDocLinks = false;
        let accessedFiles = new Set();
        let nodeIdMap = {};
        let nextNodeId = 1;
        let docLinkEdgeIds = [];

        document.addEventListener('DOMContentLoaded', function() {
            buildAgentLegend();
            initializeGraph();
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

        function initializeGraph() {
            nodes = new vis.DataSet();
            edges = new vis.DataSet();
            nodeIdMap = {};
            nextNodeId = 1;
            docLinkEdgeIds = [];

            if (showAllFiles && combinedSnapshot.files.length > 0) {
                buildFullTree();
            }

            const container = document.getElementById('graph');
            network = new vis.Network(container, { nodes: nodes, edges: edges }, {
                layout: {
                    hierarchical: {
                        enabled: false
                    }
                },
                physics: {
                    enabled: true,
                    stabilization: { iterations: 100 },
                    barnesHut: { gravitationalConstant: -2000, springLength: 100 }
                },
                nodes: {
                    shape: 'box',
                    font: { size: 10 },
                    margin: 5
                },
                edges: {
                    arrows: { to: { enabled: true, scaleFactor: 0.5 } },
                    smooth: { type: 'cubicBezier' }
                }
            });

            updateStepDisplay();
        }

        function buildFullTree() {
            // Add folder nodes
            for (const folder of combinedSnapshot.folders) {
                const parts = folder.split('/');
                const label = '[' + parts[parts.length - 1] + ']';
                const id = getNodeId(folder);

                nodes.add({
                    id: id,
                    label: label,
                    title: folder,
                    color: '#FFFACD',
                    shape: 'box'
                });

                // Add edge to parent folder
                if (parts.length > 1) {
                    const parentPath = parts.slice(0, -1).join('/');
                    const parentId = getNodeId(parentPath);
                    edges.add({
                        from: parentId,
                        to: id,
                        color: { color: '#CCCCCC' },
                        dashes: false,
                        arrows: ''
                    });
                }
            }

            // Add file nodes
            for (const file of combinedSnapshot.files) {
                const parts = file.split('/');
                const label = parts[parts.length - 1];
                const id = getNodeId(file);
                const folderPath = parts.slice(0, -1).join('/');

                nodes.add({
                    id: id,
                    label: label,
                    title: file,
                    color: '#F5F5F5',
                    shape: 'box'
                });

                // Add edge to parent folder
                if (folderPath) {
                    const parentId = nodeIdMap[folderPath.toLowerCase()];
                    if (parentId) {
                        edges.add({
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
                addDocLinks();
            }
        }

        function addDocLinks() {
            for (const [source, targets] of Object.entries(combinedSnapshot.doc_links)) {
                const sourceId = nodeIdMap[source.toLowerCase()];
                if (!sourceId) continue;

                for (const target of targets) {
                    const targetId = nodeIdMap[target.toLowerCase()];
                    if (!targetId) continue;

                    const edgeId = edges.add({
                        from: sourceId,
                        to: targetId,
                        color: { color: '#4169E1' },
                        dashes: true,
                        arrows: 'to'
                    })[0];
                    docLinkEdgeIds.push(edgeId);
                }
            }
        }

        function removeDocLinks() {
            for (const edgeId of docLinkEdgeIds) {
                try { edges.remove(edgeId); } catch(e) {}
            }
            docLinkEdgeIds = [];
        }

        function getNodeId(path) {
            const key = path.toLowerCase();
            if (!nodeIdMap[key]) {
                nodeIdMap[key] = nextNodeId++;
            }
            return nodeIdMap[key];
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
                const pathLower = event.Path.toLowerCase();
                accessedFiles.add(pathLower);

                let nodeId = nodeIdMap[pathLower];

                // Create node if not exists (for files not in snapshot)
                if (!nodeId) {
                    nodeId = getNodeId(event.Path);
                    const parts = event.Path.split('/');
                    nodes.add({
                        id: nodeId,
                        label: parts[parts.length - 1],
                        title: event.Path,
                        color: getEventColor(event.EventType),
                        shape: 'box'
                    });
                } else {
                    // Flash the node with event color
                    const flashColor = getEventColor(event.EventType);
                    nodes.update({ id: nodeId, color: flashColor });

                    // Reset color after delay
                    setTimeout(() => {
                        const baseColor = accessedFiles.has(pathLower) ? '#E8E8E8' : '#F5F5F5';
                        nodes.update({ id: nodeId, color: baseColor });
                    }, 400);
                }

                network.selectNodes([nodeId]);
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

        function toggleShowAll() {
            showAllFiles = document.getElementById('showAll').checked;
            rebuildGraph();
        }

        function toggleDocLinks() {
            showDocLinks = document.getElementById('showDocLinks').checked;
            if (showDocLinks) {
                addDocLinks();
            } else {
                removeDocLinks();
            }
        }

        function rebuildGraph() {
            nodes.clear();
            edges.clear();
            nodeIdMap = {};
            nextNodeId = 1;
            docLinkEdgeIds = [];

            if (showAllFiles && combinedSnapshot.files.length > 0) {
                buildFullTree();
            }

            // Replay events up to current step to restore state
            accessedFiles.clear();
            document.getElementById('eventLog').innerHTML = '';
            for (let i = 0; i < currentStep; i++) {
                processEvent(mergedTimeline[i]);
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
                rebuildGraph();
                updateStepDisplay();
            }
        }

        function stepFirst() {
            currentStep = 0;
            rebuildGraph();
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
            rebuildGraph();
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
