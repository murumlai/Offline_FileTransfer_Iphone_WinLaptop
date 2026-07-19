namespace OfflineFileTransfer.Core.Transfers;

/// <summary>
/// Progress snapshot reported during a transfer.
/// </summary>
public sealed record TransferProgress
{
    /// <summary>Number of files completed (any terminal status) so far.</summary>
    public int FilesCompleted { get; init; }

    /// <summary>Total number of files in the transfer.</summary>
    public int FilesTotal { get; init; }

    /// <summary>Name of the file currently being copied.</summary>
    public string? CurrentFileName { get; init; }

    /// <summary>Bytes copied for the current file.</summary>
    public long CurrentFileBytesCopied { get; init; }

    /// <summary>Total bytes for the current file, when known.</summary>
    public long? CurrentFileBytesTotal { get; init; }

    /// <summary>Cumulative bytes copied across all files so far.</summary>
    public long TotalBytesCopied { get; init; }

    /// <summary>Fraction of files completed in the range [0, 1].</summary>
    public double FileFraction => FilesTotal <= 0
        ? 0d
        : Math.Clamp((double)FilesCompleted / FilesTotal, 0d, 1d);
}
