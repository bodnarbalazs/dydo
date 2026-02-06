namespace DynaDocs.Services;

using DynaDocs.Models;

/// <summary>
/// Service for capturing project state snapshots at session claim time.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Captures a complete snapshot of the project state.
    /// Includes git-tracked files, folder structure, and doc-to-doc links.
    /// </summary>
    /// <param name="basePath">The project root directory.</param>
    /// <returns>A ProjectSnapshot containing files, folders, and doc links.</returns>
    ProjectSnapshot CaptureSnapshot(string basePath);
}
