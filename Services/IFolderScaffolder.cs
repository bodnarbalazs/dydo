namespace DynaDocs.Services;

public interface IFolderScaffolder
{
    /// <summary>
    /// Scaffold the dydo documentation tree.
    /// </summary>
    void Scaffold(string basePath);

    /// <summary>
    /// Copy all built-in templates to _system/templates/ for project-local customization.
    /// </summary>
    void CopyBuiltInTemplates(string basePath);
}
