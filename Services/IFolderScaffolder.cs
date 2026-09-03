namespace DynaDocs.Services;

public interface IFolderScaffolder
{
    /// <summary>
    /// Scaffold the dydo documentation tree.
    /// </summary>
    void Scaffold(string basePath);
}
