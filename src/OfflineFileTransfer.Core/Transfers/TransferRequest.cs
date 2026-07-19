using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Transfers;

/// <summary>
/// A request to copy one or more remote files to a local destination folder.
/// </summary>
public sealed class TransferRequest
{
    public TransferRequest(
        IReadOnlyList<RemoteFileItem> items,
        string destinationFolder,
        bool preserveStructure = false,
        DuplicatePolicy duplicatePolicy = DuplicatePolicy.AutoRename)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            throw new ArgumentException("Destination folder is required.", nameof(destinationFolder));
        }

        DestinationFolder = destinationFolder;
        PreserveStructure = preserveStructure;
        DuplicatePolicy = duplicatePolicy;
    }

    /// <summary>Files selected for download.</summary>
    public IReadOnlyList<RemoteFileItem> Items { get; }

    /// <summary>Local Windows folder to write files into.</summary>
    public string DestinationFolder { get; }

    /// <summary>When true, mirror the remote folder structure under the destination.</summary>
    public bool PreserveStructure { get; }

    /// <summary>How to handle destination name collisions.</summary>
    public DuplicatePolicy DuplicatePolicy { get; }
}
