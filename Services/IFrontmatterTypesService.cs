namespace DynaDocs.Services;

public interface IFrontmatterTypesService
{
    /// <summary>
    /// The merged set of valid frontmatter types: the built-in baseline
    /// (<see cref="DynaDocs.Models.Frontmatter.ValidTypes"/>) unioned with any
    /// user-added entries from <c>dydo/_system/types.json</c>. Cached for the
    /// lifetime of this instance — construct one per check run.
    /// </summary>
    IReadOnlyList<string> GetValidTypes();
}
