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
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path")
        {
            DefaultValueFactory = _ => null,
            Description = "Path filter (e.g., /2025 for year 2025)"
        };

        var listOption = new Option<bool>("--list", "List available sessions");
        var sessionOption = new Option<string?>("--session", "Show details for a specific session ID");

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
            var configService = new ConfigService();

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
        // Serialize sessions to JSON for embedding
        var sessionsJson = JsonSerializer.Serialize(sessions, DydoDefaultJsonContext.Default.ListAuditSession);

        // Using StringBuilder to avoid complex escaping issues with JavaScript in raw strings
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <title>Audit Replay - DynaDocs</title>");
        sb.AppendLine("    <script src=\"https://unpkg.com/vis-network/standalone/umd/vis-network.min.js\"></script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <h1>Audit Replay</h1>");
        sb.AppendLine();
        sb.AppendLine("    <table border=\"1\" cellpadding=\"5\">");
        sb.AppendLine("        <tr>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                <label>Session: </label>");
        sb.AppendLine("                <select id=\"sessionSelect\" onchange=\"loadSession()\"></select>");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                <button onclick=\"stepBack()\">&lt;</button>");
        sb.AppendLine("                <button onclick=\"togglePlay()\" id=\"playBtn\">Play</button>");
        sb.AppendLine("                <button onclick=\"stepForward()\">&gt;</button>");
        sb.AppendLine("                <button onclick=\"reset()\">Reset</button>");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                <label>Speed: </label>");
        sb.AppendLine("                <input type=\"range\" id=\"speed\" min=\"100\" max=\"2000\" value=\"500\">");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td>");
        sb.AppendLine("                Step: <span id=\"stepNum\">0</span> / <span id=\"totalSteps\">0</span>");
        sb.AppendLine("            </td>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine();
        sb.AppendLine("    <table border=\"1\" cellpadding=\"5\" style=\"margin-top: 10px;\">");
        sb.AppendLine("        <tr>");
        sb.AppendLine("            <td style=\"width: 70%; height: 500px; vertical-align: top;\">");
        sb.AppendLine("                <div id=\"graph\" style=\"width: 100%; height: 100%;\"></div>");
        sb.AppendLine("            </td>");
        sb.AppendLine("            <td style=\"width: 30%; vertical-align: top;\">");
        sb.AppendLine("                <div id=\"eventLog\" style=\"height: 500px; overflow-y: auto; font-family: monospace; font-size: 12px;\"></div>");
        sb.AppendLine("            </td>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine();
        sb.AppendLine("    <h3>Session Info</h3>");
        sb.AppendLine("    <table border=\"1\" cellpadding=\"5\" id=\"sessionInfo\">");
        sb.AppendLine("        <tr><td>Agent</td><td id=\"infoAgent\">-</td></tr>");
        sb.AppendLine("        <tr><td>Human</td><td id=\"infoHuman\">-</td></tr>");
        sb.AppendLine("        <tr><td>Started</td><td id=\"infoStarted\">-</td></tr>");
        sb.AppendLine("        <tr><td>Git HEAD</td><td id=\"infoGitHead\">-</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine();
        sb.AppendLine("    <script>");
        sb.AppendLine($"        const sessionsData = {sessionsJson};");
        sb.AppendLine();
        sb.AppendLine(GetJavaScript());
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GetJavaScript() => """
        let currentSession = null;
        let currentStep = 0;
        let playing = false;
        let playInterval = null;
        let network = null;
        let nodes = null;
        let edges = null;
        let nodeIds = {};
        let lastNodeId = null;

        document.addEventListener('DOMContentLoaded', function() {
            const select = document.getElementById('sessionSelect');
            sessionsData.forEach(function(s, i) {
                const opt = document.createElement('option');
                opt.value = i;
                opt.text = (s.agent || 'Unknown') + ' - ' + s.started.substring(0, 10) + ' (' + s.events.length + ' events)';
                select.add(opt);
            });
            if (sessionsData.length > 0) { loadSession(); }
        });

        function loadSession() {
            const idx = parseInt(document.getElementById('sessionSelect').value);
            currentSession = sessionsData[idx];
            currentStep = 0;
            lastNodeId = null;
            nodeIds = {};

            document.getElementById('infoAgent').textContent = currentSession.agent || '-';
            document.getElementById('infoHuman').textContent = currentSession.human || '-';
            document.getElementById('infoStarted').textContent = currentSession.started;
            document.getElementById('infoGitHead').textContent = currentSession.git_head || '-';
            document.getElementById('totalSteps').textContent = currentSession.events.length;

            nodes = new vis.DataSet();
            edges = new vis.DataSet();

            const container = document.getElementById('graph');
            network = new vis.Network(container, { nodes: nodes, edges: edges }, {
                physics: { stabilization: false },
                nodes: { shape: 'box', font: { size: 10 } },
                edges: { arrows: 'to' }
            });

            document.getElementById('eventLog').innerHTML = '';
            updateStepDisplay();
        }

        function updateStepDisplay() {
            document.getElementById('stepNum').textContent = currentStep;
        }

        function getNodeColor(eventType) {
            switch(eventType) {
                case 'Read': return '#a0d8ef';
                case 'Write': return '#90EE90';
                case 'Edit': return '#FFD700';
                case 'Delete': return '#FF6B6B';
                case 'Bash': return '#DDA0DD';
                case 'Blocked': return '#FF0000';
                default: return '#cccccc';
            }
        }

        function processEvent(event) {
            const log = document.getElementById('eventLog');
            const time = event.ts.substring(11, 19);
            const line = document.createElement('div');
            line.textContent = time + ' ' + event.event + ' ' + (event.path || event.cmd || event.role || '');
            line.style.backgroundColor = currentStep % 2 === 0 ? '#f0f0f0' : '#ffffff';
            log.appendChild(line);
            log.scrollTop = log.scrollHeight;

            if (event.path) {
                const path = event.path;
                let nodeId = nodeIds[path];

                if (!nodeId) {
                    nodeId = Object.keys(nodeIds).length + 1;
                    nodeIds[path] = nodeId;
                    nodes.add({
                        id: nodeId,
                        label: path.split('/').pop(),
                        title: path,
                        color: getNodeColor(event.event)
                    });
                } else {
                    nodes.update({ id: nodeId, color: getNodeColor(event.event) });
                }

                if (lastNodeId && lastNodeId !== nodeId) {
                    edges.add({ from: lastNodeId, to: nodeId, color: { color: '#888888' } });
                }

                lastNodeId = nodeId;
                network.selectNodes([nodeId]);
            }
        }

        function stepForward() {
            if (currentSession && currentStep < currentSession.events.length) {
                processEvent(currentSession.events[currentStep]);
                currentStep++;
                updateStepDisplay();
            }
        }

        function stepBack() {
            if (currentStep > 0) {
                const targetStep = currentStep - 1;
                loadSession();
                while (currentStep < targetStep) { stepForward(); }
            }
        }

        function togglePlay() {
            playing = !playing;
            document.getElementById('playBtn').textContent = playing ? 'Pause' : 'Play';

            if (playing) {
                const speed = parseInt(document.getElementById('speed').value);
                playInterval = setInterval(function() {
                    if (currentStep >= currentSession.events.length) {
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
            loadSession();
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
