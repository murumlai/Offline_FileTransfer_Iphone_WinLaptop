namespace OfflineFileTransfer.Core.IO;

/// <summary>
/// Minimal file-system abstraction so transfer logic can be unit tested without touching disk.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    /// <summary>Opens or creates a file for writing, truncating any existing content.</summary>
    Stream OpenWrite(string path);

    void DeleteFile(string path);
}
