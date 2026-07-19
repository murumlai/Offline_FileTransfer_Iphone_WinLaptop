namespace OfflineFileTransfer.Core.Files;

/// <summary>
/// Helpers for normalizing remote paths and building safe local destination paths.
/// Remote paths use forward slashes; local paths use the OS separator.
/// </summary>
public static class PathUtilities
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Normalizes a remote path to use single forward slashes, trims leading/trailing slashes,
    /// and collapses repeated separators.
    /// </summary>
    public static string NormalizeRemotePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join('/', segments);
    }

    /// <summary>
    /// Joins remote path segments with forward slashes, ignoring empty segments.
    /// </summary>
    public static string CombineRemote(params string[] segments)
    {
        var cleaned = segments
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Replace('\\', '/').Trim('/'));

        return NormalizeRemotePath(string.Join('/', cleaned));
    }

    /// <summary>
    /// Replaces characters that are invalid in a Windows file name with an underscore.
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "_";
        }

        var chars = fileName.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidFileNameChars, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        var result = new string(chars).Trim();
        return result.Length == 0 ? "_" : result;
    }

    /// <summary>
    /// Builds a local destination path. When <paramref name="preserveStructure"/> is true,
    /// the remote folder structure (relative to its source root) is mirrored under the destination.
    /// Guards against path traversal by sanitizing every segment.
    /// </summary>
    public static string BuildLocalDestinationPath(
        string destinationRoot,
        string remotePath,
        bool preserveStructure)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            throw new ArgumentException("Destination root is required.", nameof(destinationRoot));
        }

        var normalized = NormalizeRemotePath(remotePath);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("Remote path is empty.", nameof(remotePath));
        }

        var safeSegments = segments
            .Select(SanitizeFileName)
            .Where(s => s != "." && s != "..")
            .ToArray();

        if (!preserveStructure)
        {
            return Path.Combine(destinationRoot, safeSegments[^1]);
        }

        var combined = Path.Combine(new[] { destinationRoot }.Concat(safeSegments).ToArray());
        return combined;
    }

    /// <summary>
    /// Produces a non-colliding local path by appending " (n)" before the extension.
    /// Uses the provided existence check so it can be unit tested without touching disk.
    /// </summary>
    public static string ResolveUniquePath(string desiredPath, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);

        if (!exists(desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        for (var counter = 1; counter < int.MaxValue; counter++)
        {
            var candidateName = $"{name} ({counter}){extension}";
            var candidate = Path.Combine(directory, candidateName);
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to resolve a unique destination path.");
    }
}
