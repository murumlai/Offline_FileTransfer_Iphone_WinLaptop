namespace OfflineFileTransfer.Core.Models;

/// <summary>
/// Base type for any item in the normalized browse tree.
/// </summary>
public abstract record RemoteItem
{
    /// <summary>Provider-specific stable identifier (WPD object id, AFC path, etc.).</summary>
    public required string Id { get; init; }

    /// <summary>Display name of the item (file or folder name).</summary>
    public required string Name { get; init; }

    /// <summary>Normalized forward-slash remote path, e.g. "CameraRoll/IMG_0001.HEIC".</summary>
    public required string RemotePath { get; init; }

    /// <summary>Which iPhone storage surface this item came from.</summary>
    public FileSourceKind Source { get; init; } = FileSourceKind.Unknown;

    /// <summary>True when the item represents a folder.</summary>
    public abstract bool IsFolder { get; }
}
