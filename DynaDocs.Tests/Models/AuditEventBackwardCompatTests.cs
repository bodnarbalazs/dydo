namespace DynaDocs.Tests.Models;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// PR3 of agent-crash-fixes: pins the wire-format compatibility guarantee that pre-PR3 audit
/// JSON (which lacks <c>recovery_kind</c>, <c>resume_predecessor_session</c>, and
/// <c>resume_attempts_at_claim</c>) deserializes cleanly with all three new fields = null.
/// Without this test the cross-version invariant is implicit; balazs explicitly asked for it
/// before approving PR3 because the Linux CI green check only proves "new code on new data,"
/// not "new code on old data."
/// </summary>
public class AuditEventBackwardCompatTests
{
    [Fact]
    public void PrePR3ClaimEvent_DeserializesCleanly_WithNewFieldsNull()
    {
        // Real-shape Claim event sample taken from dydo/_system/audit/2026/ (e.g. the
        // 2026-04-11 Charlie session). Pre-PR3 builds emitted exactly these three keys.
        var prePR3Json = """
            {
              "ts": "2026-04-11T11:25:08.9983266Z",
              "event": "Claim",
              "agent": "Charlie"
            }
            """;

        var evt = JsonSerializer.Deserialize(prePR3Json, DydoDefaultJsonContext.Default.AuditEvent);

        Assert.NotNull(evt);
        Assert.Equal(AuditEventType.Claim, evt.EventType);
        Assert.Equal("Charlie", evt.AgentName);
        Assert.Null(evt.RecoveryKind);
        Assert.Null(evt.ResumePredecessorSession);
        Assert.Null(evt.ResumeAttemptsAtClaim);
    }

    [Fact]
    public void PrePR3SidecarLine_DeserializesCleanly_WithNewFieldsNull()
    {
        // Sidecar lines use CompactJsonContext (single-line, no indent). Older sidecar
        // entries omit the new fields entirely. The same null-defaulting must hold or
        // dydo replay-audit would crash on legacy data.
        var prePR3Sidecar = """{"ts":"2026-04-11T11:25:08.9983266Z","event":"Claim","agent":"Charlie"}""";

        var evt = JsonSerializer.Deserialize(prePR3Sidecar, CompactJsonContext.Default.AuditEvent);

        Assert.NotNull(evt);
        Assert.Null(evt.RecoveryKind);
        Assert.Null(evt.ResumePredecessorSession);
        Assert.Null(evt.ResumeAttemptsAtClaim);
    }

    [Fact]
    public void RecoveryKindFields_RoundTrip_PreservesValues()
    {
        var original = new AuditEvent
        {
            Timestamp = new DateTime(2026, 5, 7, 21, 30, 14, DateTimeKind.Utc),
            EventType = AuditEventType.Claim,
            AgentName = "Brian",
            RecoveryKind = "auto",
            ResumePredecessorSession = "f9936e33-aaaa-bbbb-cccc-1234567890ab",
            ResumeAttemptsAtClaim = 2
        };

        var json = JsonSerializer.Serialize(original, DydoDefaultJsonContext.Default.AuditEvent);
        var roundTripped = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AuditEvent);

        Assert.NotNull(roundTripped);
        Assert.Equal("auto", roundTripped.RecoveryKind);
        Assert.Equal("f9936e33-aaaa-bbbb-cccc-1234567890ab", roundTripped.ResumePredecessorSession);
        Assert.Equal(2, roundTripped.ResumeAttemptsAtClaim);
    }

    [Fact]
    public void RecoveryKindFields_NotEmitted_WhenNull()
    {
        // JsonIgnoreCondition.WhenWritingNull on each new field — null fields must be
        // absent from the output JSON, not present as "field": null. This is the
        // contract that bounds audit-log size growth on non-recovery events.
        var evt = new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = AuditEventType.Read,
            Path = "src/foo.cs"
        };

        var json = JsonSerializer.Serialize(evt, DydoDefaultJsonContext.Default.AuditEvent);

        Assert.DoesNotContain("recovery_kind", json);
        Assert.DoesNotContain("resume_predecessor_session", json);
        Assert.DoesNotContain("resume_attempts_at_claim", json);
    }

    [Theory]
    [InlineData("fresh")]
    [InlineData("auto")]
    [InlineData("manual")]
    public void RecoveryKind_AcceptsAllThreeBuckets(string kind)
    {
        var evt = new AuditEvent
        {
            EventType = AuditEventType.Claim,
            AgentName = "Adele",
            RecoveryKind = kind,
            ResumePredecessorSession = kind == "fresh" ? null : "prior-sess-id",
            ResumeAttemptsAtClaim = kind == "fresh" ? null : 0
        };

        var json = JsonSerializer.Serialize(evt, DydoDefaultJsonContext.Default.AuditEvent);
        var back = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AuditEvent);

        Assert.Equal(kind, back!.RecoveryKind);
    }
}
