using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Files;

/// <summary>
/// Maps file names/extensions to a broad <see cref="FileTypeCategory"/>.
/// Extension tables are intentionally conservative and case-insensitive.
/// </summary>
public static class FileTypeClassifier
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp",
        ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".svg", ".ico",
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4", ".m4v", ".avi", ".mkv", ".wmv", ".flv", ".webm",
        ".mpg", ".mpeg", ".3gp", ".hevc",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".oga", ".wma",
        ".aiff", ".aif", ".caf", ".opus",
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".rtf", ".md", ".csv", ".pages", ".numbers", ".key",
        ".epub", ".json", ".xml", ".html", ".htm",
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".iso",
    };

    /// <summary>
    /// Returns the lowercase extension (including the dot) for a file name, or empty when none.
    /// </summary>
    public static string GetExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var ext = Path.GetExtension(fileName);
        return string.IsNullOrEmpty(ext) ? string.Empty : ext.ToLowerInvariant();
    }

    /// <summary>
    /// Classifies a file name into a broad category using its extension.
    /// </summary>
    public static FileTypeCategory Classify(string fileName)
    {
        var ext = GetExtension(fileName);
        if (ext.Length == 0)
        {
            return FileTypeCategory.Other;
        }

        if (ImageExtensions.Contains(ext)) return FileTypeCategory.Image;
        if (VideoExtensions.Contains(ext)) return FileTypeCategory.Video;
        if (AudioExtensions.Contains(ext)) return FileTypeCategory.Audio;
        if (DocumentExtensions.Contains(ext)) return FileTypeCategory.Document;
        if (ArchiveExtensions.Contains(ext)) return FileTypeCategory.Archive;

        return FileTypeCategory.Other;
    }
}
