namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Notion;

/// <summary>Three-tier token precedence (Decision 027 §2): local secret store &gt; namespaced env &gt;
/// generic env. Each test drives a temp dydo root so it never reads the machine's real secret store,
/// and saves/restores the env vars it touches. The Windows User-registry fallback on the generic tier
/// is not exercised — it depends on machine state and would mutate the user's registry.</summary>
[Collection("ConsoleOutput")]
public class NotionTokenResolverTests
{
    private static string TempDydoRoot() =>
        Path.Combine(Path.GetTempPath(), "dydo-tokres-" + Guid.NewGuid().ToString("N")[..8], "dydo");

    [Fact]
    public void Slugify_UppercasesAndReplacesNonAlphanumeric()
    {
        Assert.Equal("MY_COOL_PROJ", NotionTokenResolver.Slugify("my-cool.proj"));
        Assert.Equal("A_B_1", NotionTokenResolver.Slugify("a b#1"));
        Assert.Equal(string.Empty, NotionTokenResolver.Slugify(null));
        // Non-ASCII letters are replaced, not kept, so the env-var name stays typeable.
        Assert.Equal("CAF_", NotionTokenResolver.Slugify("café"));
    }

    [Fact]
    public void SlugFor_PrefersConfigName_ElseProjectRootDirName()
    {
        // Forward-slash paths so the directory-name split is host-OS-agnostic: on Linux a backslash is an
        // ordinary filename character, so a "C:\x\dir-name" literal would parse as one segment, not three.
        Assert.Equal("FROM_NAME", NotionTokenResolver.SlugFor(new DydoConfig { Name = "from-name" }, "/x/dir-name"));
        Assert.Equal("DIR_NAME", NotionTokenResolver.SlugFor(new DydoConfig(), "/x/dir-name"));
        Assert.Equal("DIR_NAME", NotionTokenResolver.SlugFor(null, "/home/u/dir.name/"));
    }

    [Fact]
    public void Resolve_LocalStore_WinsOverBothEnvTiers()
    {
        var dydoRoot = TempDydoRoot();
        NotionTokenStore.Write(NotionTokenStore.PathFor(dydoRoot), "local-tok");
        var config = new DydoConfig { Name = "proj" };
        WithEnv("DYDO_PROJ_NOTION_TOKEN", "namespaced-tok", () =>
        WithEnv(NotionTokenResolver.TokenEnvVar, "generic-tok", () =>
        {
            Assert.Equal("local-tok", NotionTokenResolver.Resolve(config, @"C:\x\proj", dydoRoot));
        }));
        SafeDelete(dydoRoot);
    }

    [Fact]
    public void Resolve_NamespacedEnv_WinsOverGeneric_WhenNoLocalStore()
    {
        var dydoRoot = TempDydoRoot();
        var config = new DydoConfig { Name = "proj" };
        WithEnv("DYDO_PROJ_NOTION_TOKEN", "namespaced-tok", () =>
        WithEnv(NotionTokenResolver.TokenEnvVar, "generic-tok", () =>
        {
            Assert.Equal("namespaced-tok", NotionTokenResolver.Resolve(config, @"C:\x\proj", dydoRoot));
        }));
    }

    [Fact]
    public void Resolve_NamespacedSlug_FallsBackToProjectRootDirName()
    {
        var dydoRoot = TempDydoRoot();
        // No config name -> slug derives from the project-root directory name "cool-proj" -> COOL_PROJ.
        // Forward-slash path so the directory-name split holds on Linux too (a backslash is a filename char there).
        WithEnv("DYDO_COOL_PROJ_NOTION_TOKEN", "dir-namespaced", () =>
        {
            Assert.Equal("dir-namespaced", NotionTokenResolver.Resolve(new DydoConfig(), "/x/cool-proj", dydoRoot));
        });
    }

    [Fact]
    public void Resolve_GenericEnv_WhenNoLocalStoreAndNoNamespaced()
    {
        var dydoRoot = TempDydoRoot();
        var config = new DydoConfig { Name = "proj" };
        WithEnv("DYDO_PROJ_NOTION_TOKEN", null, () =>
        WithEnv(NotionTokenResolver.TokenEnvVar, "generic-tok", () =>
        {
            Assert.Equal("generic-tok", NotionTokenResolver.Resolve(config, @"C:\x\proj", dydoRoot));
        }));
    }

    private static void WithEnv(string name, string? value, Action body)
    {
        var saved = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        try { body(); }
        finally { Environment.SetEnvironmentVariable(name, saved); }
    }

    private static void SafeDelete(string dydoRoot)
    {
        try { Directory.Delete(Path.GetDirectoryName(dydoRoot)!, true); } catch { }
    }
}
