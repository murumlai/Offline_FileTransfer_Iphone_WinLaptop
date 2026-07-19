namespace OfflineFileTransfer.Core.Transfers;

/// <summary>
/// Policy applied when a destination file with the same name already exists.
/// </summary>
public enum DuplicatePolicy
{
    /// <summary>Skip the file and leave the existing one untouched.</summary>
    Skip = 0,

    /// <summary>Overwrite the existing file.</summary>
    Overwrite = 1,

    /// <summary>Write to a new, non-colliding name such as "photo (1).jpg".</summary>
    AutoRename = 2,
}
