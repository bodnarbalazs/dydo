namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests verifying that changelog folder structure is a soft rule.
/// dydo should not enforce any specific folder structure within changelog/.
/// </summary>
[Collection("Integration")]
public class ChangelogStructureTests : IntegrationTestBase
{
    [Fact]
    public async Task Check_AcceptsFlatChangelogStructure()
    {
        // Arrange - Initialize and create changelog files directly in changelog folder (no subfolders)
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Create a flat changelog entry (no year/date subfolders)
        WriteFile("dydo/project/changelog/auth-feature.md", """
            ---
            area: project
            type: changelog
            date: 2025-01-15
            ---

            # Auth Feature

            Added authentication support.

            ## Summary

            Implemented OAuth2 authentication flow.

            ## Files Changed

            - src/auth/login.cs
            - src/auth/token.cs
            """);

        // Link the file from the changelog hub to avoid orphan warning
        var hubContent = ReadFile("dydo/project/changelog/_index.md");
        WriteFile("dydo/project/changelog/_index.md", hubContent + "\n- [Auth Feature](./auth-feature.md) - Added authentication support\n");

        // Act - Run check
        var checkResult = await CheckAsync();

        // Assert - Should pass without errors about changelog structure
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
        // The flat changelog file should be accepted
        Assert.DoesNotContain("changelog", checkResult.Stderr.ToLower());
    }

    [Fact]
    public async Task Check_AcceptsAlternativeChangelogStructure()
    {
        // Arrange - Initialize and create changelog with feature-based organization
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Create a feature-based subfolder structure (not year/date)
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo/project/changelog/feature-auth"));

        // Hub file is required for all folders with content
        WriteFile("dydo/project/changelog/feature-auth/_index.md", """
            ---
            area: project
            type: hub
            ---

            # Feature Auth

            Changelog entries related to authentication feature.

            ## Contents

            - [Initial Implementation](./initial-impl.md) - First version of auth system
            """);

        WriteFile("dydo/project/changelog/feature-auth/_feature-auth.md", """
            ---
            area: project
            type: folder-meta
            ---

            # Feature Auth

            Changelog entries related to authentication feature.
            """);

        WriteFile("dydo/project/changelog/feature-auth/initial-impl.md", """
            ---
            area: project
            type: changelog
            date: 2025-01-10
            ---

            # Initial Implementation

            First version of auth system.

            ## Summary

            Basic login/logout functionality.

            ## Files Changed

            - src/auth/login.cs
            """);

        // Link from parent hub to the subfolder
        var parentHubContent = ReadFile("dydo/project/changelog/_index.md");
        WriteFile("dydo/project/changelog/_index.md", parentHubContent + "\n- [Feature Auth](./feature-auth/_index.md) - Auth-related changes\n");

        // Act - Run check
        var checkResult = await CheckAsync();

        // Assert - Should pass (alternative structure is allowed)
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
    }

    [Fact]
    public async Task Fix_DoesNotEnforceChangelogFolderStructure()
    {
        // Arrange - Initialize and create flat changelog structure
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Create flat changelog file
        var changelogContent = """
            ---
            area: project
            type: changelog
            date: 2025-01-15
            ---

            # Some Feature

            Added a feature.

            ## Summary

            Details here.

            ## Files Changed

            - src/file.cs
            """;
        WriteFile("dydo/project/changelog/some-feature.md", changelogContent);

        // Link from hub to avoid orphan issues
        var hubContent = ReadFile("dydo/project/changelog/_index.md");
        WriteFile("dydo/project/changelog/_index.md", hubContent + "\n- [Some Feature](./some-feature.md) - Added a feature\n");

        // Act - Run fix
        var fixResult = await FixAsync();
        fixResult.AssertSuccess();

        // Assert - File should still be in flat location (not moved to year/date folders)
        AssertFileExists("dydo/project/changelog/some-feature.md");

        // Should not have created year folders
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/project/changelog/2025")),
            "dydo fix should not create year folders - structure is a soft rule");
    }

    [Fact]
    public async Task Check_AcceptsMixedChangelogStructure()
    {
        // Arrange - Initialize with both flat files and nested folders
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Flat file - link it from the changelog hub
        WriteFile("dydo/project/changelog/quick-fix.md", """
            ---
            area: project
            type: changelog
            date: 2025-01-01
            ---

            # Quick Fix

            A quick bug fix.

            ## Summary

            Fixed null reference.

            ## Files Changed

            - src/utils.cs
            """);

        // Nested in year/date (following the suggested structure)
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo/project/changelog/2025/2025-01-15"));

        // Year folder needs hub
        WriteFile("dydo/project/changelog/2025/_index.md", """
            ---
            area: project
            type: hub
            ---

            # 2025

            Changelog entries from 2025.

            ## Contents

            - [2025-01-15](./2025-01-15/_index.md) - Changes on January 15
            """);

        WriteFile("dydo/project/changelog/2025/_2025.md", """
            ---
            area: project
            type: folder-meta
            ---

            # 2025

            Changelog entries from 2025.
            """);

        // Date folder needs hub
        WriteFile("dydo/project/changelog/2025/2025-01-15/_index.md", """
            ---
            area: project
            type: hub
            ---

            # 2025-01-15

            Changes on January 15, 2025.

            ## Contents

            - [Major Release](./major-release.md) - Version 2.0 release
            """);

        WriteFile("dydo/project/changelog/2025/2025-01-15/_2025-01-15.md", """
            ---
            area: project
            type: folder-meta
            ---

            # 2025-01-15

            Changes on January 15, 2025.
            """);

        WriteFile("dydo/project/changelog/2025/2025-01-15/major-release.md", """
            ---
            area: project
            type: changelog
            date: 2025-01-15
            ---

            # Major Release

            Version 2.0 release.

            ## Summary

            Major version bump.

            ## Files Changed

            - package.json
            """);

        // Link everything from parent hub
        var parentHubContent = ReadFile("dydo/project/changelog/_index.md");
        WriteFile("dydo/project/changelog/_index.md", parentHubContent +
            "\n- [Quick Fix](./quick-fix.md) - Bug fix\n" +
            "- [2025](./2025/_index.md) - 2025 changes\n");

        // Act - Run check
        var checkResult = await CheckAsync();

        // Assert - Mixed structure should be accepted
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
    }

    #region Helper Methods

    private async Task<CommandResult> FixAsync(string? path = null)
    {
        var command = FixCommand.Create();
        var args = path != null ? new[] { path } : Array.Empty<string>();
        return await RunAsync(command, args);
    }

    #endregion
}
