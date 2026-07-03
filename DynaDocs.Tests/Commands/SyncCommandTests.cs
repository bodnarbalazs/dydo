namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using Xunit;

public class SyncCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly RoleDefinition _reviewer;

    public SyncCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-sync-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _reviewer = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "reviewer");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void SyncRole_WritesAgentAndSkillFiles()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "reviewer.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md")));
    }

    [Fact]
    public void SyncRole_Agent_HasReadOnlyToolProfileAndFrontmatter()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);
        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));

        Assert.Contains("name: reviewer", agent);
        // Read-only role → no Edit/Write tools (that's how "reviewers don't write code" is native-enforced)
        Assert.Contains("tools: Read, Grep, Glob, Bash", agent);
        Assert.DoesNotContain("Edit", agent);
        Assert.DoesNotContain("Write", agent);
        // Carries project-context must-reads
        Assert.Contains("coding-standards.md", agent);
    }

    [Fact]
    public void SyncRole_Skill_KeepsMethodology_DropsOrchestration()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md"));

        // Timeless methodology survives
        Assert.Contains("Mindset", skill);
        Assert.Contains("YOU SHALL NOT PASS", skill);
        Assert.Contains("Review checklist", skill);

        // Old-runtime orchestration is gone
        Assert.DoesNotContain("## Set Role", skill);
        Assert.DoesNotContain("## Register General Wait", skill);
        Assert.DoesNotContain("dydo wait", skill);
        Assert.DoesNotContain("dydo agent role", skill);
        // The {{AGENT_NAME}} placeholder is de-personalized
        Assert.DoesNotContain("{{AGENT_NAME}}", skill);
    }

    [Fact]
    public void ExtractMethodology_StripsFrontmatter()
    {
        var methodology = SyncCommand.ExtractMethodology(_reviewer, _testDir);
        // The mode-file frontmatter (agent:/mode:) must not leak into the skill body
        Assert.DoesNotContain("mode: reviewer", methodology);
        Assert.StartsWith("#", methodology.TrimStart());
        // No dangling horizontal rule at the end after dropping the trailing section
        Assert.False(methodology.TrimEnd().EndsWith("---"));
    }

    [Fact]
    public void SyncRole_WriterRole_GetsWriterToolsAndStance()
    {
        var codeWriter = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "code-writer");
        SyncCommand.SyncRole(codeWriter, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
        // A writer role gets Edit/Write AND writer-stance prose — not the read-only contradiction
        Assert.Contains("Edit, Write", agent);
        Assert.Contains("produce and modify the project's files", agent);
        Assert.DoesNotContain("read-only", agent);

        // The skill description must be role-correct, not reviewer-hardcoded
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "code-writer", "SKILL.md"));
        Assert.DoesNotContain("reviewing a code change", skill);
        Assert.Contains("working as a code-writer", skill);
    }

    [Fact]
    public void ExtractMustReads_IsRoleSpecific()
    {
        // docs-writer's must-reads come from ITS template (writing-docs, not coding-standards)
        var docsWriter = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "docs-writer");
        var mustReads = SyncCommand.ExtractMustReads(docsWriter, _testDir);

        Assert.Contains("dydo/reference/writing-docs.md", mustReads);
        Assert.DoesNotContain("dydo/guides/coding-standards.md", mustReads);
        // Reviewer's are different — code standards, not writing-docs
        var reviewerMustReads = SyncCommand.ExtractMustReads(_reviewer, _testDir);
        Assert.Contains("dydo/guides/coding-standards.md", reviewerMustReads);
    }

    [Fact]
    public void RenumberOrderedLists_ContinuesAcrossProse_ResetsOnHeading()
    {
        // A list interrupted by prose/code keeps numbering; a new heading restarts it.
        var input = "## A\n1. one\n2. two\n\nsome prose\n3. three\n\n## B\n1. fresh";
        var result = SyncCommand.RenumberOrderedLists(input);
        Assert.Equal("## A\n1. one\n2. two\n\nsome prose\n3. three\n\n## B\n1. fresh", result);
    }

    [Fact]
    public void RenumberOrderedLists_IgnoresContentInsideCodeFences()
    {
        // Inquisition finding: a literal "1." or "# comment" inside a ```fence``` must not
        // be renumbered or reset the running count.
        var input = "1. first\n```bash\n# a shell comment\n1. not a list item\n```\n2. second";
        var result = SyncCommand.RenumberOrderedLists(input);
        Assert.Equal("1. first\n```bash\n# a shell comment\n1. not a list item\n```\n2. second", result);
    }

    [Fact]
    public void SyncCommand_Run_GeneratesAllWorkerRoles()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            foreach (var role in new[] { "code-writer", "reviewer", "test-writer", "docs-writer", "sprint-auditor" })
            {
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{role}.md")), $"missing agent: {role}");
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", role, "SKILL.md")), $"missing skill: {role}");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void SyncCommand_Run_GeneratesPlannerSkill_ButNoPlannerAgent()
    {
        // Decision 024: planner is skill-only. `dydo sync` emits its SKILL.md so the
        // orchestrator can apply the planning methodology, but never an agent definition
        // (there is no planner sub-agent to spawn and no claimable planner identity).
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "planner", "SKILL.md")),
                "planner skill must be generated");
            Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "planner.md")),
                "planner must NOT get a native agent definition");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void SyncSkillOnlyRole_WritesSkillButNoAgent()
    {
        var planner = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "planner");
        SyncCommand.SyncSkillOnlyRole(planner, _testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "planner", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "planner.md")));
    }

    [Fact]
    public void RenumberOrderedLists_FixesDuplicateNumbering()
    {
        // A list whose numbering was broken by an included continuation (…4. then 4./5.)
        // is renumbered as a single 1..N run; blank lines don't break the run.
        var input = "1. first\n2. second\n\n2. dup\n3. next";
        var result = SyncCommand.RenumberOrderedLists(input);
        Assert.Equal("1. first\n2. second\n\n3. dup\n4. next", result);
    }

    [Fact]
    public void SyncRole_SprintAuditor_ToolAllowlistOmitsAgentEditWrite()
    {
        // Decision 026: the sprint-auditor must not be able to dispatch subagents or write
        // files — enforced natively by the tools allowlist in the generated definition.
        var auditor = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "sprint-auditor");
        SyncCommand.SyncRole(auditor, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "sprint-auditor.md"));
        var toolsLine = agent.Split('\n').Single(l => l.StartsWith("tools:"));
        Assert.Equal("tools: Read, Grep, Glob, Bash", toolsLine.TrimEnd());
    }

    [Fact]
    public void SyncRole_SprintAuditor_Skill_CarriesInquisitorJudgeCharacter()
    {
        var auditor = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "sprint-auditor");
        SyncCommand.SyncRole(auditor, _testDir);

        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "sprint-auditor", "SKILL.md"));
        // Inquisitor lens: hunts real cross-slice issues (seams are the signature concern)
        Assert.Contains("Inquisitor", skill);
        Assert.Contains("Seams", skill);
        // Judge strictness: verdict with findings, no "pass with notes"
        Assert.Contains("Judge", skill);
        Assert.Contains("pass with notes", skill);
        // Works alone — no subagent dispatch
        Assert.Contains("cannot dispatch subagents", skill);
    }

    [Fact]
    public void SyncRole_EmitsLfLineEndings()
    {
        // CRLF in .claude/ artifacts makes Claude Code's permission handler reject them
        // ("control characters that would be hidden in the approval dialog"), so sync
        // must emit LF regardless of platform/template line endings.
        SyncCommand.SyncRole(_reviewer, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md"));
        Assert.DoesNotContain('\r', agent);
        Assert.DoesNotContain('\r', skill);
    }

    [Fact]
    public void SyncCommand_Run_WritesReviewerArtifacts()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            var result = SyncCommand.Create().Parse([]).Invoke();

            Assert.Equal(0, result);
            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "reviewer.md")));
            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    // --- Model-tier resolution (Decision 028) ---

    private static ModelsConfig TestModels() => new()
    {
        Tiers = new Dictionary<string, Dictionary<string, string>>
        {
            ["anthropic"] = new() { ["strong"] = "model-strong", ["standard"] = "model-standard" }
        },
        Roles = new Dictionary<string, string>
        {
            ["reviewer"] = "strong",
            ["code-writer"] = "standard",
            ["docs-writer"] = "light" // tier NOT bound in the vendor map
        },
        Efforts = new Dictionary<string, string> { ["code-writer"] = "low" }
    };

    [Fact]
    public void ResolveModel_MappedRole_ReturnsConcreteModel()
    {
        var (model, effort) = SyncCommand.ResolveModel(TestModels(), "reviewer");
        Assert.Equal("model-strong", model);
        Assert.Null(effort);
    }

    [Fact]
    public void ResolveModel_RoleWithEffort_ReturnsBoth()
    {
        var (model, effort) = SyncCommand.ResolveModel(TestModels(), "code-writer");
        Assert.Equal("model-standard", model);
        Assert.Equal("low", effort);
    }

    [Fact]
    public void ResolveModel_UnmappedRole_ReturnsNull()
    {
        // No role → tier entry: inherit the session model (Decision 028 — no silent downgrade).
        var (model, effort) = SyncCommand.ResolveModel(TestModels(), "sprint-auditor");
        Assert.Null(model);
        Assert.Null(effort);
    }

    [Fact]
    public void ResolveModel_TierMissingFromVendorMap_ReturnsNull()
    {
        // docs-writer maps to "light", which the vendor map does not bind → inherit.
        var (model, _) = SyncCommand.ResolveModel(TestModels(), "docs-writer");
        Assert.Null(model);
    }

    [Fact]
    public void ResolveModel_AbsentModelsSection_ReturnsNull()
    {
        var (model, effort) = SyncCommand.ResolveModel(null, "reviewer");
        Assert.Null(model);
        Assert.Null(effort);
    }

    [Fact]
    public void SyncRole_WithModels_EmitsResolvedModelFrontmatter()
    {
        SyncCommand.SyncRole(_reviewer, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        Assert.Contains("\nmodel: model-strong\n", agent);
        Assert.DoesNotContain("model: inherit", agent);
        Assert.DoesNotContain("effort:", agent); // no effort configured for reviewer
    }

    [Fact]
    public void SyncRole_WithEffort_EmitsEffortLine()
    {
        var codeWriter = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "code-writer");
        SyncCommand.SyncRole(codeWriter, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
        Assert.Contains("\nmodel: model-standard\neffort: low\n", agent);
    }

    [Fact]
    public void SyncRole_UnmappedRole_FallsBackToInherit()
    {
        var auditor = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "sprint-auditor");
        SyncCommand.SyncRole(auditor, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "sprint-auditor.md"));
        Assert.Contains("model: inherit", agent);
    }

    [Fact]
    public void SyncRole_NoModelsSection_FallsBackToInherit()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        Assert.Contains("model: inherit", agent);
    }

    [Fact]
    public void SyncCommand_Run_ResolvesModelsFromDydoJson()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
                {
                  "version": 1,
                  "models": {
                    "tiers": { "anthropic": { "strong": "vendor-strong-model" } },
                    "roles": { "reviewer": "strong" }
                  }
                }
                """);
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            var reviewer = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
            Assert.Contains("model: vendor-strong-model", reviewer);
            // Unmapped worker roles inherit the session model
            var codeWriter = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
            Assert.Contains("model: inherit", codeWriter);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void DefaultModels_ResolveForAllTieredWorkerRoles()
    {
        // The shipped defaults (Decision 028) must actually bind: every role in the
        // default role → tier map resolves to a concrete model.
        var models = ConfigFactory.CreateDefaultModels();
        foreach (var role in models.Roles.Keys)
        {
            var (model, _) = SyncCommand.ResolveModel(models, role);
            Assert.False(string.IsNullOrEmpty(model), $"default tier for '{role}' did not resolve");
        }
    }

    // --- Tier-1 manager modes (Decision 026) ---

    [Fact]
    public void SyncCommand_Run_GeneratesTier1ManagerSkills_ButNoAgents()
    {
        // Decision 026: orchestrator / co-thinker / chief-of-staff mode skills join the
        // sync output, but Tier-1 identities are terminal sessions, never sub-agents.
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            foreach (var role in new[] { "orchestrator", "co-thinker", "chief-of-staff" })
            {
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", role, "SKILL.md")),
                    $"missing Tier-1 skill: {role}");
                Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{role}.md")),
                    $"Tier-1 mode '{role}' must NOT get a native agent definition");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Theory]
    [InlineData("orchestrator")]
    [InlineData("co-thinker")]
    [InlineData("chief-of-staff")]
    public void Tier1ManagerSkills_StateTheManagersDoctrine(string roleName)
    {
        // Decision 026 §1–2: every Tier-1 mode skill states the doctrine — managers
        // write no code, implementation goes through workflows, trivial-edit exception.
        var role = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == roleName);
        SyncCommand.SyncSkillOnlyRole(role, _testDir);

        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", roleName, "SKILL.md"));
        Assert.Contains("Managers Doctrine", skill);
        Assert.Contains("run-sprint", skill);
        Assert.Contains("if it needs a reviewer, it needs a workflow", skill);
    }

    [Fact]
    public void ChiefOfStaff_Skill_CarriesCharacterAndInvariants()
    {
        var chief = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "chief-of-staff");
        SyncCommand.SyncSkillOnlyRole(chief, _testDir);

        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "chief-of-staff", "SKILL.md"));
        // The human's right hand: triage + routing, status reports, mediation
        Assert.Contains("right hand", skill);
        Assert.Contains("Triage the Funnel", skill);
        Assert.Contains("Status Reports", skill);
        Assert.Contains("Escalations awaiting decisions", skill);
        Assert.Contains("Gates awaiting the human", skill);
        Assert.Contains("Mediate Between Agents", skill);
        // Invariants: never in an approval path; PM objects/docs, never code
        Assert.Contains("never in an approval path", skill);
        Assert.Contains("never code", skill);
    }

    [Fact]
    public void OrchestratorTemplate_HasNoDead10Machinery()
    {
        // Decision 026 sweep: worker-tier dispatch, .needs-merge markers, and
        // `dydo worktree merge` flows are 1.0 machinery that no longer exists.
        var methodology = SyncCommand.ExtractMethodology(
            RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "orchestrator"), _testDir);

        Assert.DoesNotContain(".needs-merge", methodology);
        Assert.DoesNotContain("dydo worktree merge", methodology);
        Assert.DoesNotContain("--role inquisitor", methodology);
        Assert.DoesNotContain("--role code-writer", methodology);
        // The 2.0 reality is stated instead
        Assert.Contains("run-sprint", methodology);
    }

    [Fact]
    public void CoThinkerTemplate_DoesNotSwitchIntoCodeWriting()
    {
        var raw = TemplateGenerator.ReadBuiltInTemplate("mode-co-thinker.template.md");

        Assert.DoesNotContain("dydo agent role code-writer", raw);
        Assert.DoesNotContain("--role planner", raw);
        Assert.Contains("run-sprint", raw);
    }
}
