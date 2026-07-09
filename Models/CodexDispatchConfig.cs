namespace DynaDocs.Models;

using System.Text.Json.Serialization;
using DynaDocs.Services;

/// <summary>
/// Codex launch posture (issue 0253). Every dispatched codex command line carries
/// <c>--sandbox &lt;mode&gt; --ask-for-approval &lt;policy&gt;</c> so the session runs under sandbox
/// enforcement without a human hand-approving every action — the sandbox is the boundary,
/// approval prompts only to exceed it (co-think, balazs 2026-07-09). An absent
/// <c>dispatch.codex</c> section resolves to the shipped defaults in
/// <see cref="ConfigFactory"/>, never a bare launch. The dangerous-bypass flag
/// (<c>--dangerously-bypass-approvals-and-sandbox</c> / <c>--yolo</c>) is deliberately not
/// representable here — it is never emitted and not configurable.
/// </summary>
public class CodexDispatchConfig
{
    /// <summary>Accepted <c>--sandbox</c> modes (codex CLI reference, verified 2026-07-09).</summary>
    public static readonly string[] AcceptedSandboxValues =
        ["read-only", "workspace-write", "danger-full-access"];

    /// <summary>
    /// Accepted <c>--ask-for-approval</c> policies (codex CLI reference, verified 2026-07-09).
    /// <c>on-failure</c> is DEPRECATED in the codex CLI and deliberately excluded — never emitted.
    /// </summary>
    public static readonly string[] AcceptedApprovalPolicyValues =
        ["untrusted", "on-request", "never"];

    [JsonPropertyName("sandbox")]
    public string Sandbox { get; set; } = ConfigFactory.DefaultCodexSandbox;

    [JsonPropertyName("approvalPolicy")]
    public string ApprovalPolicy { get; set; } = ConfigFactory.DefaultCodexApprovalPolicy;

    /// <summary>
    /// True when the configured sandbox mode enforces the codex Windows OS-level sandbox, whose
    /// per-machine provisioning (<c>codex-windows-sandbox-setup.exe</c>) is a dispatch prerequisite
    /// on Windows. <c>workspace-write</c> — the shipped default — runs under that elevated sandbox;
    /// <c>read-only</c> and <c>danger-full-access</c> do not require the provisioning, so the
    /// dispatch preflight's sandbox check does not fire for them.
    /// </summary>
    [JsonIgnore]
    public bool RequiresWindowsSandbox => Sandbox == "workspace-write";

    /// <summary>
    /// Returns one message per invalid posture value, each naming the accepted list. Empty when
    /// the posture is well-formed.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!AcceptedSandboxValues.Contains(Sandbox))
            errors.Add($"dispatch.codex.sandbox '{Sandbox}' is invalid (accepted: {string.Join(", ", AcceptedSandboxValues)})");
        if (!AcceptedApprovalPolicyValues.Contains(ApprovalPolicy))
            errors.Add($"dispatch.codex.approvalPolicy '{ApprovalPolicy}' is invalid (accepted: {string.Join(", ", AcceptedApprovalPolicyValues)})");
        return errors;
    }
}
